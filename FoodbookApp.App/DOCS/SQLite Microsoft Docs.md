The SQLite database engine allows .NET Multi-platform App UI (.NET MAUI) apps to load and save data objects in shared code. You can integrate SQLite.NET into .NET MAUI apps, to store and retrieve information in a local database, by following these steps:

    Install the NuGet package.
    Configure constants.
    Create TodoItem class.
    Create a database access class.
    Access data.
    Advanced configuration.

This article uses the sqlite-net-pcl NuGet package to provide SQLite database access to a table to store todo items. An alternative is to use the Microsoft.Data.Sqlite NuGet package, which is a lightweight ADO.NET provider for SQLite. Microsoft.Data.Sqlite implements the common ADO.NET abstractions for functionality such as connections, commands, and data readers.
Install the SQLite NuGet package

Use the NuGet package manager to search for the sqlite-net-pcl package and add the latest version to your .NET MAUI app project.

There are a number of NuGet packages with similar names. The correct package has these attributes:

    ID: sqlite-net-pcl
    Authors: SQLite-net
    Owners: praeclarum
    NuGet link: sqlite-net-pcl

Despite the package name, use the sqlite-net-pcl NuGet package in .NET MAUI projects.

Important

SQLite.NET is a third-party library that's supported from the praeclarum/sqlite-net repo.
Configure app constants

Configuration data, such as database filename and path, can be stored as constants in your app. The sample project includes a Constants.cs file that provides common configuration data:
C#

public static class Constants
{
    public const string DatabaseFilename = "TodoSQLite.db3";

    public const SQLite.SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLite.SQLiteOpenFlags.ReadWrite |
        // create the database if it doesn't exist
        SQLite.SQLiteOpenFlags.Create |
        // enable multi-threaded database access
        SQLite.SQLiteOpenFlags.SharedCache;

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}

In this example, the constants file specifies default SQLiteOpenFlag enum values that are used to initialize the database connection. The SQLiteOpenFlag enum supports these values:

    Create: The connection will automatically create the database file if it doesn't exist.
    FullMutex: The connection is opened in serialized threading mode.
    NoMutex: The connection is opened in multi-threading mode.
    PrivateCache: The connection will not participate in the shared cache, even if it's enabled.
    ReadWrite: The connection can read and write data.
    SharedCache: The connection will participate in the shared cache, if it's enabled.
    ProtectionComplete: The file is encrypted and inaccessible while the device is locked.
    ProtectionCompleteUnlessOpen: The file is encrypted until it's opened but is then accessible even if the user locks the device.
    ProtectionCompleteUntilFirstUserAuthentication: The file is encrypted until after the user has booted and unlocked the device.
    ProtectionNone: The database file isn't encrypted.

You may need to specify different flags depending on how your database will be used. For more information about SQLiteOpenFlags, see Opening A New Database Connection on sqlite.org.
Create TodoItem class

Before coding the database access, create the class that will store the data.
C#

public class TodoItem
{
    [PrimaryKey, AutoIncrement]
    public int ID { get; set; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public bool Done { get; set; }
}

The ID in this class will serve as the primary key that is auto-incremented upon save. Attributes can be used to specify this behavior.
Create a database access class

A database wrapper class abstracts the data access layer from the rest of the app. This class centralizes query logic and simplifies the management of database initialization, making it easier to refactor or expand data operations as the app grows. The sample app defines a TodoItemDatabase class for this purpose.
Lazy initialization

The TodoItemDatabase uses asynchronous lazy initialization to delay initialization of the database until it's first accessed, with a simple Init method that gets called by each method in the class:
C#

public class TodoItemDatabase
{
    SQLiteAsyncConnection database;

    async Task Init()
    {
        if (database is not null)
            return;

        database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
        var result = await database.CreateTableAsync<TodoItem>();
    }
    ...
}

Data manipulation methods

The TodoItemDatabase class includes methods for the four types of data manipulation: create, read, edit, and delete. The SQLite.NET library provides a simple Object Relational Map (ORM) that allows you to store and retrieve objects without writing SQL statements.

The following example shows the data manipulation methods in the sample app:
C#

public class TodoItemDatabase
{
    ...
    public async Task<List<TodoItem>> GetItemsAsync()
    {
        await Init();
        return await database.Table<TodoItem>().ToListAsync();
    }

    public async Task<List<TodoItem>> GetItemsNotDoneAsync()
    {
        await Init();
        return await database.Table<TodoItem>().Where(t => t.Done).ToListAsync();

        // SQL queries are also possible
        //return await Database.QueryAsync<TodoItem>("SELECT * FROM [TodoItem] WHERE [Done] = 0");
    }

    public async Task<TodoItem> GetItemAsync(int id)
    {
        await Init();
        return await database.Table<TodoItem>().Where(i => i.ID == id).FirstOrDefaultAsync();
    }

    public async Task<int> SaveItemAsync(TodoItem item)
    {
        await Init();
        if (item.ID != 0)
            return await database.UpdateAsync(item);
        else
            return await database.InsertAsync(item);
    }

    public async Task<int> DeleteItemAsync(TodoItem item)
    {
        await Init();
        return await database.DeleteAsync(item);
    }
}

Access data

The TodoItemDatabase class can be registered as a singleton that can be used throughout the app if you are using dependency injection. For example, you can register your pages and the database access class as services on the IServiceCollection object, in MauiProgram.cs, with the AddSingleton and AddTransient methods:
C#

builder.Services.AddSingleton<TodoListPage>();
builder.Services.AddTransient<TodoItemPage>();

builder.Services.AddSingleton<TodoItemDatabase>();

These services can then be automatically injected into class constructors, and accessed:
C#

TodoItemDatabase database;

public TodoItemPage(TodoItemDatabase todoItemDatabase)
{
    InitializeComponent();
    database = todoItemDatabase;
}

async void OnSaveClicked(object sender, EventArgs e)
{
    if (string.IsNullOrWhiteSpace(Item.Name))
    {
        await DisplayAlertAsync("Name Required", "Please enter a name for the todo item.", "OK");
        return;
    }

    await database.SaveItemAsync(Item);
    await Shell.Current.GoToAsync("..");
}

Alternatively, new instances of the database access class can be created:
C#

TodoItemDatabase database;

public TodoItemPage()
{
    InitializeComponent();
    database = new TodoItemDatabase();
}

For more information about dependency injection in .NET MAUI apps, see Dependency injection.
Advanced configuration

SQLite provides a robust API with more features than are covered in this article and the sample app. The following sections cover features that are important for scalability.

For more information, see SQLite Documentation on sqlite.org.
Write-ahead logging

By default, SQLite uses a traditional rollback journal. A copy of the unchanged database content is written into a separate rollback file, then the changes are written directly to the database file. The COMMIT occurs when the rollback journal is deleted.

Write-Ahead Logging (WAL) writes changes into a separate WAL file first. In WAL mode, a COMMIT is a special record, appended to the WAL file, which allows multiple transactions to occur in a single WAL file. A WAL file is merged back into the database file in a special operation called a checkpoint.

WAL can be faster for local databases because readers and writers do not block each other, allowing read and write operations to be concurrent. However, WAL mode doesn't allow changes to the page size, adds additional file associations to the database, and adds the extra checkpointing operation.

To enable WAL in SQLite.NET, call the EnableWriteAheadLoggingAsync method on the SQLiteAsyncConnection instance:
C#

await Database.EnableWriteAheadLoggingAsync();

For more information, see SQLite Write-Ahead Logging on sqlite.org.
Copy a database

There are several cases where it may be necessary to copy a SQLite database:

    A database has shipped with your application but must be copied or moved to writeable storage on the mobile device.
    You need to make a backup or copy of the database.
    You need to version, move, or rename the database file.

In general, moving, renaming, or copying a database file is the same process as any other file type with a few additional considerations:

    All database connections should be closed before attempting to move the database file.
    If you use Write-Ahead Logging, SQLite will create a Shared Memory Access (.shm) file and a (Write Ahead Log) (.wal) file. Ensure that you apply any changes to these files as well.


    samples:

    ﻿using SQLite;
using TodoSQLite.Models;

namespace TodoSQLite.Data;

public class TodoItemDatabase
{
    SQLiteAsyncConnection database;

    async Task Init()
    {
        if (database is not null)
            return;

        database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
        var result = await database.CreateTableAsync<TodoItem>();
    }

    public async Task<List<TodoItem>> GetItemsAsync()
    {
        await Init();
        return await database.Table<TodoItem>().ToListAsync();
    }

    public async Task<List<TodoItem>> GetItemsNotDoneAsync()
    {
        await Init();
        return await database.Table<TodoItem>().Where(t => t.Done).ToListAsync();
        
        // SQL queries are also possible
        //return await database.QueryAsync<TodoItem>("SELECT * FROM [TodoItem] WHERE [Done] = 0");
    }

    public async Task<TodoItem> GetItemAsync(int id)
    {
        await Init();
        return await database.Table<TodoItem>().Where(i => i.ID == id).FirstOrDefaultAsync();
    }

    public async Task<int> SaveItemAsync(TodoItem item)
    {
        await Init();
        if (item.ID != 0)
        {
            return await database.UpdateAsync(item);
        }
        else
        {
            return await database.InsertAsync(item);
        }
    }

    public async Task<int> DeleteItemAsync(TodoItem item)
    {
        await Init();
        return await database.DeleteAsync(item);
    }


}


maui program:
﻿using TodoSQLite.Data;
using TodoSQLite.Views;

namespace TodoSQLite;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<TodoListPage>();
		builder.Services.AddTransient<TodoItemPage>();
		builder.Services.AddSingleton<TodoItemDatabase>();

		return builder.Build();
	}


}


constants:
﻿namespace TodoSQLite;

public static class Constants
{
    public const string DatabaseFilename = "TodoSQLite.db3";

    public const SQLite.SQLiteOpenFlags Flags =
        // open the database in read/write mode
        SQLite.SQLiteOpenFlags.ReadWrite |
        // create the database if it doesn't exist
        SQLite.SQLiteOpenFlags.Create |
        // enable multi-threaded database access
        SQLite.SQLiteOpenFlags.SharedCache;

    public static string DatabasePath => 
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}

csproj:
﻿<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
		<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
		<!-- Uncomment to also build the tizen app. You will need to install tizen by following this: https://github.com/Samsung/Tizen.NET -->
		<!-- <TargetFrameworks>$(TargetFrameworks);net10.0-tizen</TargetFrameworks> -->

		<!-- Note for MacCatalyst:
		The default runtime is maccatalyst-x64, except in Release config, in which case the default is maccatalyst-x64;maccatalyst-arm64.
		When specifying both architectures, use the plural <RuntimeIdentifiers> instead of the singular <RuntimeIdentifier>.
		The Mac App Store will NOT accept apps with ONLY maccatalyst-arm64 indicated;
		either BOTH runtimes must be indicated or ONLY macatalyst-x64. -->
		<!-- For example: <RuntimeIdentifiers>maccatalyst-x64;maccatalyst-arm64</RuntimeIdentifiers> -->

		<OutputType>Exe</OutputType>
		<RootNamespace>TodoSQLite</RootNamespace>
		<UseMaui>true</UseMaui>
		<SingleProject>true</SingleProject>
		<ImplicitUsings>enable</ImplicitUsings>

		<!-- Display name -->
		<ApplicationTitle>TodoSQLite</ApplicationTitle>

		<!-- App Identifier -->
		<ApplicationId>com.companyname.todosqlite</ApplicationId>

		<!-- Versions -->
		<ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
		<ApplicationVersion>1</ApplicationVersion>

		<!-- To develop, package, and publish an app to the Microsoft Store, see: https://aka.ms/MauiTemplateUnpackaged -->
		<WindowsPackageType>None</WindowsPackageType>

		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">15.0</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">15.0</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">21.0</SupportedOSPlatformVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
		<TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</TargetPlatformMinVersion>
		<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'tizen'">6.5</SupportedOSPlatformVersion>
	</PropertyGroup>

	<ItemGroup>
		<!-- App Icon -->
		<MauiIcon Include="Resources\AppIcon\appicon.svg" ForegroundFile="Resources\AppIcon\appiconfg.svg" Color="#512BD4" />

		<!-- Splash Screen -->
		<MauiSplashScreen Include="Resources\Splash\splash.svg" Color="#512BD4" BaseSize="128,128" />

		<!-- Images -->
		<MauiImage Include="Resources\Images\*" />
		<MauiImage Update="Resources\Images\dotnet_bot.png" Resize="True" BaseSize="300,185" />

		<!-- Custom Fonts -->
		<MauiFont Include="Resources\Fonts\*" />

		<!-- Raw Assets (also remove the "Resources\Raw" prefix) -->
		<MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
	</ItemGroup>

	<ItemGroup>
	  <None Remove="Resources\Images\add.svg" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
		<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
		<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0-preview.3.25171.5" />
	</ItemGroup>

</Project>