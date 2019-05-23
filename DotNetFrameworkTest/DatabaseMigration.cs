using DotNetFrameworkDataLayer;
using System;
using System.Configuration;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations.Design;
using System.IO;
using System.Text.RegularExpressions;

namespace DotNetFrameworkTest
{
    public static class DatabaseMigration
    {
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["AppConnectionString"].ConnectionString;

        public static void RefreshDb()
        {
            using (var dbContext = new AppDbContext())
            {
                DbConnectionInfo connectionStringInfo = new DbConnectionInfo(
                    ConnectionString, "System.Data.SqlClient"); // We shoud retrieve this from App.config

                ToolingFacade toolingFacade = new ToolingFacade(
                    "DotNetFrameworkDataLayer", // MigrationAssemblyName. In this case dll should be located in "C:\\Temp\\MigrationTest" dir
                    "DotNetFrameworkDataLayer", // ContextAssemblyName. Same as above
                    null,
                    AppDomain.CurrentDomain.BaseDirectory, // Where the dlls are located
                    AppDomain.CurrentDomain.BaseDirectory +
                    "\\App.config", // Insert the right directory and change with Web.config if required
                    AppDomain.CurrentDomain.BaseDirectory + "\\App_Data",
                    connectionStringInfo)
                {
                    LogInfoDelegate = s => { Console.WriteLine(s); },
                    LogWarningDelegate = s => { Console.WriteLine("WARNING: " + s); },
                    LogVerboseDelegate = s => { Console.WriteLine("VERBOSE: " + s); }
                };

                var scriptUpdate = toolingFacade.ScriptUpdate(null, null, true);

                if (!string.IsNullOrEmpty(scriptUpdate))
                {
                    ScaffoldedMigration scaffoldedMigration =
                        toolingFacade.Scaffold("AutoMigrationCode", "C#", "DotNetFrameworkDataLayer", false);

                    var fileName = GetFileName(scriptUpdate);

                    //Create Directory to insert .cs and .sql file
                    Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName);
                    File.WriteAllText(
                        AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName + "\\" + fileName + ".cs",
                        scaffoldedMigration.UserCode);

                    File.WriteAllText(
                        AppDomain.CurrentDomain.BaseDirectory + "\\Migrations\\" + fileName + "\\" + fileName + ".sql",
                        scriptUpdate);

                    //if (!Database.CreateIfNotExists())
                    //{
                    dbContext.Database.ExecuteSqlCommand(scriptUpdate);
                    //}
                }
                else
                {
                    //If there is no changes only make sure we have created database
                    dbContext.Database.CreateIfNotExists();
                }
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
}
