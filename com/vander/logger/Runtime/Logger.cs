using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Vander.Logger
{
    /// <summary>
    /// Logger centralisé : console Unity + fichier + filtres (niveau, catégories, sources/classes) + verbose debug.
    /// Utilisation recommandée par classe : private static readonly var Log = Logger.For<MyClass>(LogCategory.X);
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly StringBuilder _sb = new StringBuilder(256);

        // ----------------------------
        // Runtime config (modifiable)
        // ----------------------------
        public static bool Enabled { get; private set; } = true;

        /// <summary>Contrôle uniquement les logs Debug</summary>
        public static bool VerboseLogs { get; private set; } = true;

        public static bool UnityConsoleEnabled { get; private set; } = true;
        public static LogLevel MinLevel { get; private set; } = LogLevel.Info;

        // --- Category filtering ---
        // Si AllowList non vide => seules ces catégories passent.
        private static readonly HashSet<LogCategory> _categoryAllowList = new HashSet<LogCategory>();

        // --- Source filtering (per class/file name) ---
        // Si SourceAllowList non vide => seules ces sources passent.
        // SourceDenyList coupe même si allowlist est vide.
        private static readonly HashSet<string> _sourceAllowList = new HashSet<string>();
        private static readonly HashSet<string> _sourceDenyList = new HashSet<string>();

        // ----------------------------
        // File logging
        // ----------------------------
        public static bool FileLoggingEnabled { get; private set; } = true;
        public static string LogFilePath { get; private set; }

        private static StreamWriter _writer;

        // ----------------------------
        // Scoped logger (par classe)
        // ----------------------------
        public readonly struct ScopedLogger
        {
            private readonly string _source;
            private readonly LogCategory _category;

            public ScopedLogger(string source, LogCategory category)
            {
                _source = source;
                _category = category;
            }

            public void Debug(string message) => Logger.LogInternal(LogLevel.Debug, _category, _source, message, null);
            public void Info(string message) => Logger.LogInternal(LogLevel.Info, _category, _source, message, null);
            public void Warning(string message) => Logger.LogInternal(LogLevel.Warning, _category, _source, message, null);
            public void Error(string message, Exception ex = null) => Logger.LogInternal(LogLevel.Error, _category, _source, message, ex);
        }

        public static ScopedLogger For<T>(LogCategory category = LogCategory.General)
            => new ScopedLogger(typeof(T).Name, category);

        public static ScopedLogger For(string source, LogCategory category = LogCategory.General)
            => new ScopedLogger(string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim(), category);

        // ----------------------------
        // Configuration
        // ----------------------------
        public static void Configure(LoggerSettings settings)
        {
            if (settings == null)
            {
                Apply(
                    enabled: true,
                    verboseLogs: true,
                    unityConsoleEnabled: true,
                    minLevel: LogLevel.Info,
                    fileLoggingEnabled: true,
                    logFileName: "app.log",
                    allowedCategories: null,
                    allowedSources: null,
                    deniedSources: null
                );
                return;
            }

            Apply(
                enabled: settings.enabled,
                verboseLogs: settings.verboseLogs,
                unityConsoleEnabled: settings.unityConsoleEnabled,
                minLevel: settings.minLevel,
                fileLoggingEnabled: settings.fileLoggingEnabled,
                logFileName: settings.logFileName,
                allowedCategories: settings.allowedCategories,
                allowedSources: settings.allowedSources,
                deniedSources: settings.deniedSources
            );
        }

        public static void Apply(
            bool enabled,
            bool verboseLogs,
            bool unityConsoleEnabled,
            LogLevel minLevel,
            bool fileLoggingEnabled,
            string logFileName,
            IEnumerable<LogCategory> allowedCategories,
            IEnumerable<string> allowedSources,
            IEnumerable<string> deniedSources
        )
        {
            Enabled = enabled;
            VerboseLogs = verboseLogs;
            UnityConsoleEnabled = unityConsoleEnabled;
            MinLevel = minLevel;
            FileLoggingEnabled = fileLoggingEnabled;

            lock (_lock)
            {
                // Categories
                _categoryAllowList.Clear();
                if (allowedCategories != null)
                {
                    foreach (var c in allowedCategories)
                        _categoryAllowList.Add(c);
                }

                // Sources allow/deny
                _sourceAllowList.Clear();
                if (allowedSources != null)
                {
                    foreach (var s in allowedSources)
                        AddNonEmptyTrimmed(_sourceAllowList, s);
                }

                _sourceDenyList.Clear();
                if (deniedSources != null)
                {
                    foreach (var s in deniedSources)
                        AddNonEmptyTrimmed(_sourceDenyList, s);
                }

                // File writer
                CloseWriter_NoLock();

                if (!FileLoggingEnabled) return;
                var safeName = string.IsNullOrWhiteSpace(logFileName) ? "app.log" : logFileName.Trim();
                LogFilePath = Path.Combine(Application.persistentDataPath, safeName);

                try
                {
                    Directory.CreateDirectory(Application.persistentDataPath);
                    _writer = new StreamWriter(LogFilePath, append: true, encoding: Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    WriteLine_NoLock($"===== Session start {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
                    WriteLine_NoLock($"persistentDataPath: {Application.persistentDataPath}");
                    WriteLine_NoLock($"unityVersion: {Application.unityVersion}");
                }
                catch (Exception ex)
                {
                    _writer = null;
                    FileLoggingEnabled = false;

                    if (UnityConsoleEnabled)
                        UnityEngine.Debug.LogError($"[Logger] Failed to open log file: {ex}");
                }
            }
        }

        private static void AddNonEmptyTrimmed(HashSet<string> set, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            set.Add(value.Trim());
        }

        // ----------------------------
        // Public toggles (global)
        // ----------------------------
        public static void SetEnabled(bool enabled) => Enabled = enabled;
        public static void SetVerbose(bool verboseLogs) => VerboseLogs = verboseLogs;
        public static void SetMinLevel(LogLevel level) => MinLevel = level;
        public static void SetUnityConsoleEnabled(bool enabled) => UnityConsoleEnabled = enabled;

        // ----------------------------
        // Category filtering
        // ----------------------------
        public static void SetAllowedCategories(IEnumerable<LogCategory> categories)
        {
            lock (_lock)
            {
                _categoryAllowList.Clear();
                if (categories == null) return;
                foreach (var c in categories) _categoryAllowList.Add(c);
            }
        }

        public static void AddAllowedCategory(LogCategory category)
        {
            lock (_lock) { _categoryAllowList.Add(category); }
        }

        public static void ClearAllowedCategories()
        {
            lock (_lock) { _categoryAllowList.Clear(); }
        }

        // ----------------------------
        // Source filtering (per class)
        // ----------------------------
        public static void SetAllowedSources(IEnumerable<string> sources)
        {
            lock (_lock)
            {
                _sourceAllowList.Clear();
                if (sources == null) return;
                foreach (var s in sources) AddNonEmptyTrimmed(_sourceAllowList, s);
            }
        }

        public static void AddAllowedSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return;
            lock (_lock) { _sourceAllowList.Add(source.Trim()); }
        }

        public static void ClearAllowedSources()
        {
            lock (_lock) { _sourceAllowList.Clear(); }
        }

        public static void SetDeniedSources(IEnumerable<string> sources)
        {
            lock (_lock)
            {
                _sourceDenyList.Clear();
                if (sources == null) return;
                foreach (var s in sources) AddNonEmptyTrimmed(_sourceDenyList, s);
            }
        }

        public static void AddDeniedSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return;
            lock (_lock) { _sourceDenyList.Add(source.Trim()); }
        }

        public static void ClearDeniedSources()
        {
            lock (_lock) { _sourceDenyList.Clear(); }
        }

        // ----------------------------
        // Simple API (sans scope)
        // ----------------------------
        public static void Debug(LogCategory category, string message) => LogInternal(LogLevel.Debug, category, "General", message, null);
        public static void Info(LogCategory category, string message) => LogInternal(LogLevel.Info, category, "General", message, null);
        public static void Warning(LogCategory category, string message) => LogInternal(LogLevel.Warning, category, "General", message, null);
        public static void Error(LogCategory category, string message, Exception ex = null) => LogInternal(LogLevel.Error, category, "General", message, ex);

        public static void Debug(string message) => Debug(LogCategory.General, message);
        public static void Info(string message) => Info(LogCategory.General, message);
        public static void Warning(string message) => Warning(LogCategory.General, message);
        public static void Error(string message, Exception ex = null) => Error(LogCategory.General, message, ex);

        // ----------------------------
        // Shutdown
        // ----------------------------
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_writer != null)
                    WriteLine_NoLock($"===== Session end {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");

                CloseWriter_NoLock();
            }
        }

        // ----------------------------
        // Core
        // ----------------------------
        internal static void LogInternal(LogLevel level, LogCategory category, string source, string message, Exception ex)
        {
            if (!Enabled) return;

            // VerboseLogs contrôle uniquement Debug
            if (level == LogLevel.Debug && !VerboseLogs) return;

            if (level < MinLevel) return;

            if (!IsCategoryAllowed(category)) return;

            var safeSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim();
            if (!IsSourceAllowed(safeSource)) return;

            var line = FormatLine(level, category, safeSource, message, ex);

            // Console Unity
            if (UnityConsoleEnabled)
            {
                switch (level)
                {
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(line);
                        break;
                    case LogLevel.Error:
                        UnityEngine.Debug.LogError(line);
                        break;
                    default:
                        UnityEngine.Debug.Log(line);
                        break;
                }
            }

            // File
            if (!FileLoggingEnabled) return;
            lock (_lock)
            {
                if (_writer != null)
                    WriteLine_NoLock(line);
            }
        }

        private static bool IsCategoryAllowed(LogCategory category)
        {
            lock (_lock)
            {
                // allowlist vide => tout passe
                return _categoryAllowList.Count == 0 || _categoryAllowList.Contains(category);
            }
        }

        private static bool IsSourceAllowed(string source)
        {
            lock (_lock)
            {
                if (_sourceDenyList.Contains(source)) return false;

                // allowlist vide => tout passe (sauf denylist)
                return _sourceAllowList.Count == 0 || _sourceAllowList.Contains(source);
            }
        }

        private static string FormatLine(LogLevel level, LogCategory category, string source, string message, Exception ex)
        {
            // 2026-02-23 13:02:15.123 [INFO] [Geo] [GeoJsonLoader] message
            _sb.Clear();
            // _sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            _sb.Append("[SIG_VIEWER]");
            _sb.Append(" [");
            _sb.Append(level.ToString().ToUpperInvariant());
            _sb.Append("] [");
            _sb.Append(category);
            _sb.Append("] [");
            _sb.Append(source);
            _sb.Append("] ");
            _sb.Append(message);

            if (ex == null) return _sb.ToString();
            _sb.Append(" | EX: ");
            _sb.Append(ex.GetType().Name);
            _sb.Append(": ");
            _sb.Append(ex.Message);
            _sb.Append("\n");
            _sb.Append(ex.StackTrace);

            return _sb.ToString();
        }

        private static void WriteLine_NoLock(string line)
        {
            try
            {
                _writer.WriteLine(line);
            }
            catch
            {
                // Si l’écriture échoue, on coupe le file logging pour éviter du spam d’exceptions.
                FileLoggingEnabled = false;
                CloseWriter_NoLock();
            }
        }

        private static void CloseWriter_NoLock()
        {
            try { _writer?.Flush(); } catch { /* ignore */ }
            try { _writer?.Dispose(); } catch { /* ignore */ }
            _writer = null;
        }
    }
}