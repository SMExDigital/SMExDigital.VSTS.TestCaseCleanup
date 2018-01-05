using Fclp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Console = Colorful.Console;
using System.Drawing;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.TestManagement.WebApi;

namespace SMExDigital.VSTS.TestCaseCleanup
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var cliParser = new FluentCommandLineParser<TestCaseCleanupOptions>();
            var options = SetupAndParseArguments(args, cliParser);
            if (options)
            {
                cliParser.HelpOption.ShowHelp(cliParser.Options);
                return;
            }

            MainAsync(cliParser.Object).GetAwaiter().GetResult();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static async Task MainAsync(TestCaseCleanupOptions options)
        {
            Console.WriteLineFormatted("Connecting to {0}...", Color.SkyBlue, Color.Gray, options.Uri);

            var uri = new Uri(options.Uri);
            var credentials = new VssBasicCredential("", options.PersonalAccessToken);
            var connection = new VssConnection(uri, credentials);

            using (var projectClient = await connection.GetClientAsync<ProjectHttpClient>().ConfigureAwait(false))
            using (var witClient = await connection.GetClientAsync<WorkItemTrackingHttpClient>().ConfigureAwait(false))
            using (var tmClient = await connection.GetClientAsync<TestManagementHttpClient>().ConfigureAwait(false))
            {
                Console.WriteLineFormatted("Retrievig project {0}...", Color.SkyBlue, Color.Gray, options.ProjectName);
                var project = await projectClient.GetProject(options.ProjectName).ConfigureAwait(false);

                if (project == null)
                {
                    Console.WriteLineFormatted("Project {0} not found", Color.DarkRed, Color.Red, options.ProjectName);
                    return;
                }

                var wiqlQUery = new Wiql()
                {
                    Query = $"SELECT * FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' And [System.TeamProject] = '{project.Name}'"
                };

                Console.WriteLine("Running query...");
                var results = await witClient.QueryByWiqlAsync(wiqlQUery).ConfigureAwait(false);
                Console.WriteLineFormatted("Found {0} test cases", Color.SkyBlue, Color.Gray, results.WorkItems.Count());

                var workItemIds = results.WorkItems.Select(x => x.Id).ToArray();

                Console.WriteLine("Retrieving work item details...");
                var wiBatchTasks = workItemIds.Batch(50)
                    .Select(x => witClient.GetWorkItemsAsync(x.ToArray(), new[] { "System.Id", "System.Title", "Microsoft.VSTS.TCM.Steps", "System.CreatedBy", "System.CreatedDate" }))
                    .ToArray();

                Console.WriteLine("Looking for tests with no steps...");
                var wiResults = await Task.WhenAll(wiBatchTasks).ConfigureAwait(false);

                // Find ones with no test steps
                var wiWithNoSteps = wiResults.SelectMany(x => x).Where(x => !x.Fields.ContainsKey("Microsoft.VSTS.TCM.Steps"));
                Console.WriteLineFormatted("Found {0} test cases with no steps", Color.Red, Color.Gray, wiWithNoSteps.Count());

                var deleteTasks = new List<Task>();

                foreach (var wi in wiWithNoSteps)
                {
                    Console.WriteLineFormatted("[{0}] {1}", Color.SkyBlue, Color.Gray, wi.Id, wi.Fields["System.Title"]);
                    if (options.DeleteTestCases)
                    {
                        deleteTasks.Add(tmClient.DeleteTestCaseAsync(project.Id, wi.Id.Value));
                    }
                }

                if (options.DeleteTestCases)
                {
                    Console.WriteLineFormatted("Deleting {0} test cases with no steps", Color.Red, Color.Gray, wiWithNoSteps.Count());
                    await Task.WhenAll(deleteTasks).ConfigureAwait(false);
                    Console.WriteLineFormatted("{0} test cases deleted", Color.Red, Color.Gray, wiWithNoSteps.Count());
                }
            }
        }

        private static bool SetupAndParseArguments(string[] args, FluentCommandLineParser<TestCaseCleanupOptions> cliParser)
        {
            cliParser.Setup(x => x.Uri)
                            .As('u', "uri")
                            .WithDescription("Uri of the TFS/VSTS collection e.g. https://myaccount.visualstudio.com")
                            .Required();
            cliParser.Setup(x => x.ProjectName)
                .As('p', "projectName")
                .WithDescription("Name of the Project to example")
                .Required();
            cliParser.Setup(x => x.PersonalAccessToken)
                .As('t', "token")
                .WithDescription("Personal Access Token to connect to the VSTS instance")
                .Required();
            cliParser.Setup(x => x.DeleteTestCases)
                .As('d', "delete")
                .WithDescription("Flag to delete test cases with no steps (default false)")
                .SetDefault(false);
            cliParser.SetupHelp("?", "help")
                .Callback(t => Console.WriteLine(t));

            var options = cliParser.Parse(args);
            return options.HasErrors;
        }
    }

    public class TestCaseCleanupOptions
    {
        public string Uri { get; set; }
        public string ProjectName { get; set; }
        public string PersonalAccessToken { get; set; }
        public bool DeleteTestCases { get; set; }
    }
}
