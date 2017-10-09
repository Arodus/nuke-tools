using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nuke.Core;
using Octokit;
using Octokit.Internal;

public static class ReferencePullRequest
{
    const string K_prMessage = "Update References";

    public static void CreatePullRequestIfNonExists(string targetRepositoryOwner, string targetRepositoryName,
        string currentBranch, string gitHubAccessToken)
    {
        targetRepositoryName =
            Regex.Replace(targetRepositoryName, @"\.git$",
                ""); //Todo remove after https://github.com/nuke-build/nuke/pull/8 is merged
        CreatePullRequestIfNonExistsAsync(targetRepositoryOwner, targetRepositoryName, currentBranch, gitHubAccessToken)
            .Wait();
    }

    static async Task CreatePullRequestIfNonExistsAsync(string targetRepositoryOwner,
        string targetRepositoryName, string currentBranch, string gitHubAccessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("nuke.build"),
            new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

        try
        {
            var repo = await client.Repository.Get(targetRepositoryOwner, targetRepositoryName);

            var repoId = repo.Id;
            var pullRequests = await client.PullRequest.GetAllForRepository(repoId);
            if (!pullRequests.Any(x =>
                x.State == ItemState.Open && !x.Merged && x.Head.Label == $"{targetRepositoryOwner}:{currentBranch}" &&
                x.Title == K_prMessage && x.Base.Label == $"{targetRepositoryOwner}:master"))
                await client.PullRequest.Create(repoId,
                    new NewPullRequest(K_prMessage, $"{targetRepositoryOwner}:{currentBranch}", "master"));
        }
        catch (Exception e)
        {
            ControlFlow.Fail("Could not find Repository: {0}", e.Message);
        }
    }
}