# Vander Logger

Custom logging system for Unity\
Console + fichier + filtres avancés (niveau, catégories, sources) +
scoped logging.

------------------------------------------------------------------------

## Features

-   Logs vers console Unity
-   Logs vers fichier (Application.persistentDataPath)
-   Filtrage par niveau, catégorie, source
-   Scoped logger par classe
-   Configuration via ScriptableObject
-   Thread-safe

------------------------------------------------------------------------

## Installation

Dans `Packages/manifest.json` :

``` json
{
  "dependencies": {
    "com.vander.logger": "file:../com.vander.logger"
  }
}
```

------------------------------------------------------------------------

## Utilisation

``` csharp
private static readonly Logger.ScopedLogger Log =
    Logger.For<MyClass>(LogCategory.System);

Log.Info("Initialized");
```

------------------------------------------------------------------------

## Configuration

Créer un asset :

Create → Utils → Logger Settings

Puis utiliser LoggerBootstrapper.

------------------------------------------------------------------------

## Auteur

Arnaud Vander
