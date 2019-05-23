using PluginStructure.Infrastructure;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DotNetFrameworkDataLayer
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() : base("AppConnectionString")
        {
            //This line will make every initialization null
            //therefore no one can change database from outside
            Database.SetInitializer<AppDbContext>(null);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //create virtual app domain to make sure .dll files are not in using mode and we can remove it easily from project 
            //even if we have console application
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            setup.DisallowApplicationBaseProbing = false;
            setup.DisallowBindingRedirects = false;
            AppDomain domain = AppDomain.CreateDomain("DataLayer AppDomain", null, setup);

            //find and get all .dll files near app
            var assembly = new List<Assembly>();
            if (Directory.Exists(AppDomain.CurrentDomain.BaseDirectory))
            {
                string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory);

                foreach (string file in files)
                {
                    if (file.EndsWith(".dll"))
                    {
                        Type type = typeof(Proxy);
                        var value = (Proxy)domain.CreateInstanceAndUnwrap(
                            type.Assembly.FullName,
                            type.FullName);

                        assembly.Add(value.GetAssembly(Path.GetFullPath(file)));
                    }
                }
            }

            // Adding All entities dynamically to dbcontext
            var types = assembly.SelectMany(x => x.GetTypes())
            .Where(x => !string.IsNullOrEmpty(x.Namespace))
            .Where(x => x.BaseType != null && x.BaseType == typeof(BaseEntity))
            .ToList();

            var method = typeof(DbModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => x.Name == "Entity" && x.IsGenericMethod)
                .Single(x => x.ReturnType != typeof(DbModelBuilder));

            foreach (var type in types)
            {
                method.MakeGenericMethod(type).Invoke(modelBuilder, new object[] { });
            }

            AppDomain.Unload(domain);
        }

        public void RefreshDb()
        {
            //Create directory if it's not exists
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\"))
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\");

            //Create database Connection info based on Connection string which is set in app.config
            DbConnectionInfo connectionStringInfo = new DbConnectionInfo(
                 ConfigurationManager.ConnectionStrings["AppConnectionString"].ConnectionString, "System.Data.SqlClient"); // We shoud retrieve this from App.config

            //Get Current Assembly name
            //This is important because our dbcontext is in this assembly
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

            //Create tooling face to stimulate Entity framework tools
            ToolingFacade toolingFacade = new ToolingFacade(
                assemblyName, // MigrationAssemblyName. In this case dll should be located in "C:\\Temp\\MigrationTest" dir
                assemblyName, // ContextAssemblyName. Same as above
                null,
                AppDomain.CurrentDomain.BaseDirectory, // Where the dlls are located
                AppDomain.CurrentDomain.BaseDirectory +
                "\\App.config", // Insert the right directory and change with Web.config if required
                AppDomain.CurrentDomain.BaseDirectory + "\\App_Data",
                connectionStringInfo)
            {
                //If we want to log our database changes we can write our logger here
                //LogInfoDelegate = s => { Console.WriteLine(s); },
                //LogWarningDelegate = s => { Console.WriteLine("WARNING: " + s); },
                //LogVerboseDelegate = s => { Console.WriteLine("VERBOSE: " + s); }
            };

            //create sql script of currently changes
            var scriptUpdate = toolingFacade.ScriptUpdate(null, null, true);

            //if any changes exists then Create Change files and also update-database
            if (!string.IsNullOrEmpty(scriptUpdate))
            {
                ScaffoldedMigration scaffoldedMigration =
                    toolingFacade.Scaffold("AutoMigrationCode", "C#", assemblyName, false);

                var fileName = GetFileName(scriptUpdate);

                //Create Directory to insert .cs and .sql file
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName);
                File.WriteAllText(
                    AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName + "\\" + fileName + ".cs",
                    scaffoldedMigration.UserCode);

                //Write logs to Migrations folder
                File.WriteAllText(
                    AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName + "\\" + fileName + ".sql",
                    scriptUpdate);

                //Run script to migrate database to latest version
                Database.ExecuteSqlCommand(scriptUpdate);
            }
            else
            {
                //If there is no changes only make sure we have created database
                Database.CreateIfNotExists();
            }
        }

        private static string GetFileName(string scriptUpdate)
        {
            string re1 = ".*?"; // Non-greedy match on filler
            string re2 = "(\\'.*?\\')"; // Single Quote String 1

            Regex r = new Regex(re1 + re2, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Match m = r.Match(scriptUpdate);
            if (m.Success)
            {
                String strng1 = m.Groups[1].ToString();
                return strng1.Replace("'", "");
            }

            return null;
        }
    }

    public class Proxy : MarshalByRefObject
    {
        public Assembly GetAssembly(string assemblyPath)
        {
            try
            {
                return Assembly.LoadFile(assemblyPath);
            }
            catch (Exception)
            {
                return null;
                // throw new InvalidOperationException(ex);
            }
        }
    }
}
