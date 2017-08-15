﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octo.Commands;
using Serilog;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands
{
    [Command("list-releases", Description = "List releases by project")]
    public class ListReleasesCommand : ApiCommand, ISupportFormattedOutput
    {
        readonly HashSet<string> projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<ProjectResource> projectResources;
        private string[] projectsFilter;
        List<ReleaseResource> releases;

        public ListReleasesCommand(IOctopusAsyncRepositoryFactory repositoryFactory, ILogger log, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, log, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Listing");
            options.Add("project=", "Name of a project to filter by. Can be specified many times.", v => projects.Add(v));
        }

        public async Task Query()
        {
            projectResources = new List<ProjectResource>();
            projectsFilter = new string[0];

            if (projects.Count > 0)
            {
                LogDebug("Loading projects...");
                //var test = Repository.Projects.FindByNames(projects.ToArray());
                projectResources = await Repository.Projects.FindByNames(projects.ToArray()).ConfigureAwait(false);
                projectsFilter = projectResources.Select(p => p.Id).ToArray();
            }

            LogDebug("Loading releases...");
            
            releases = await Repository.Releases
                .FindMany(x => projectsFilter.Contains(x.ProjectId))
                .ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            Log.Information("Releases: {Count}", releases.Count);
            foreach (var project in projectResources)
            {
                Log.Information(" - Project: {Project:l}", project.Name);

                foreach (var release in releases.Where(x => x.ProjectId == project.Id))
                {
                    var propertiesToLog = new List<string>();
                    propertiesToLog.AddRange(FormatReleasePropertiesAsStrings(release));
                    foreach (var property in propertiesToLog)
                    {
                        Log.Information("    {Property:l}", property);
                    }
                    Log.Information("");
                }
            }
        }

        public void PrintJsonOutput()
        {
            Log.Information(JsonConvert.SerializeObject(projectResources.Select(pr => new
            {
                pr.Name,
                Releases = releases.Where(r => r.ProjectId == pr.Id).Select(r => new
                {
                    r.Version,
                    r.Assembled,
                    PackageVersions = GetPackageVersionsAsString(r.SelectedPackages),
                    ReleaseNotes = !string.IsNullOrEmpty(r.ReleaseNotes) ? r.ReleaseNotes.Replace(Environment.NewLine, @"\n") : string.Empty

                })
            }), Formatting.Indented));
        }

        public void PrintXmlOutput()
        {
            throw new NotImplementedException();
        }
    }
}
