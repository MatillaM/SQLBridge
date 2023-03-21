#SQLBridge
SQLBridge is a console application built on .Net, designed to simplify the migration process of code from older database-based applications such as Oracle Forms, to .Net. Its main purpose is to convert the original database entities such as tables and views, procedures, and functions into classes and methods in a .Net project.

During this process, the original code is retained as comments within the .Net classes. This enables anyone to view the decisions made during the migration process within the same solution. The final project is able to compile, allowing both old and new code to be linked together within the same solution.

With everything under the Visual Studio umbrella, it becomes easier to explore and navigate between the old and new code, taking full advantage of Visual Studio's capabilities.
##License
SQLBridge is licensed under the GNU Public License v.3.

##Installation
To use SQLBridge, you must have .Net 6.0 or higher installed on your machine. After that, follow these steps:

1. Configure SQLBridge by modifying the appsettings.json file located in the SQLBridge project folder.
- SchemaEntitiesFilePath: Path to the file containing the schema entities (tables and views) SQL script.
- SchemaPackagesFolderPath: Path to the folder containing the packages SQL scripts.
- BaseFolderPath: Path to the folder where the SQLBridge output will be stored.
- DBSchemaName: Name of the database schema.
2. Build the solution and the code will appear in the base folder path.

##Usage
Once you have configured SQLBridge, you can use it by running the console app from the command line. SQLBridge will generate .Net classes and methods that correspond to the schema entities and packages you specified in the configuration file.

##Contributing
If you would like to contribute to SQLBridge, please create a pull request with your changes. All contributions must be licensed under the GNU Public License v.3.

##Contact
If you have any questions or feedback about SQLBridge, please contact me through this repo.