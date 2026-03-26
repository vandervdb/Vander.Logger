using System.Collections.Generic;

namespace Vander.Logger
{
    [CreateAssetMenu(menuName = "Utils/Logger Settings", fileName = "LoggerSettings")]
    public class LoggerSettings : ScriptableObject
    {
        [Header("Global")]
        public bool enabled = true;

        [Tooltip("Contrôle uniquement les logs Debug")]
        public bool verboseLogs = true;

        [Tooltip("Active/désactive l'affichage dans la console Unity")]
        public bool unityConsoleEnabled = true;

        [Header("Level filtering")]
        public LogLevel minLevel = LogLevel.Info;

        [Header("File logging")]
        public bool fileLoggingEnabled = true;

        [Tooltip("Nom du fichier dans Application.persistentDataPath")]
        public string logFileName = "app.log";

        [Header("Category allow-list")]
        [Tooltip("Si vide => toutes les catégories passent. Sinon, seules celles listées passent.")]
        public List<LogCategory> allowedCategories = new List<LogCategory>();

        [Header("Source/Class allow-list")]
        [Tooltip("Si vide => toutes les sources passent (sauf deny-list). Ex: GeoJsonLoader, CoordinateConverter")]
        public List<string> allowedSources = new List<string>();

        [Header("Source/Class deny-list")]
        [Tooltip("Sources coupées même si l'allow-list est vide. Ex: BigMeshBuilder")]
        public List<string> deniedSources = new List<string>();
    }
}