using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace GitlabUtils;

internal static class Program
{
    private const string TargetBranch = "LEM-1294";

    private static async Task Main(string[] args)
    {
        if (string.IsNullOrEmpty(TargetBranch))
        {
            Console.WriteLine("Specify target branch");
            return;
        }
        
        Console.CursorVisible = false;
        Console.Write("Getting projects...");
        var projects = await GetProjectsAsync();
        Console.WriteLine($" {projects.Count} found");
        Console.WriteLine($"Looking for branch '{TargetBranch}':");

        foreach (var (project, index) in projects.OrderBy(p => p.Id).Select((p, i) => (p, i)))
        {
            Console.Write($"{index + 1} | {project.Name} (ID: {project.Id})");
            Console.CursorLeft = 0;
            if (await BranchExistsAsync(project.Id, TargetBranch))
            {
                Console.WriteLine($"Found in project: {project.Name} (ID: {project.Id})");
            }
            Console.Write(new string(' ', Console.WindowWidth));
            Console.CursorLeft = 0;
        }
    }

    private static async Task<List<Project>> GetProjectsAsync()
    {
        var projects = new List<Project>();
        using var client = CreateHttpClient();
        var page = 1;

        while (true)
        {
            var response = await client.GetAsync($"/api/v4/projects?page={page}&per_page=100");
            if (!response.IsSuccessStatusCode) break;

            var content = await response.Content.ReadAsStringAsync();
            var pageProjects = JsonConvert.DeserializeObject<List<Project>>(content);

            if (pageProjects == null || pageProjects.Count == 0) break;

            projects.AddRange(pageProjects);
            page++;
        }

        return projects;
    }

    private static async Task<bool> BranchExistsAsync(int projectId, string branch)
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"/api/v4/projects/{projectId}/repository/branches/{branch}");
        return response.IsSuccessStatusCode;
    }

    private static HttpClient CreateHttpClient()
    {
        var conf = GetConf();
        
        var client = new HttpClient { BaseAddress = new Uri(conf.url) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", conf.token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static (string url, string token) GetConf()
    {
        var conf =  new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings_dev.json", optional: true, reloadOnChange: true)
            .Build();

        var baseUrl = conf["GitLabConfig:BaseUrl"];
        var token = conf["GitLabConfig:AccessToken"];
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Wrong Gitlab configuration");
            Environment.Exit(1);
        }
        
        return (baseUrl, token);
    }

    private class Project
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public required string Name { get; set; }
    }
}