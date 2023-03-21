using Microsoft.Extensions.Configuration;
using SQLBridge.OracleToDotNet;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var schemaEntitiesFilePath = config.GetSection("SchemaEntitiesFilePath").Value;
var schemaPackagesFolderPath = config.GetSection("SchemaPackagesFolderPath").Value;
var baseFolderPath = config.GetSection("BaseFolderPath").Value;
var schemaName = config.GetSection("DBSchemaName").Value;

if (baseFolderPath == null) throw new DirectoryNotFoundException();

var entitiesPath = baseFolderPath + "/Entities";
var codePath = baseFolderPath + "/Code";

new ProcessEntities(schemaEntitiesFilePath, entitiesPath, schemaName);

new ProcessPackages(schemaPackagesFolderPath, codePath, schemaName);
