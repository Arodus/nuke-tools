using System;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Core;
using Octokit;
using Octokit.Internal;

public static class ReferencePullRequest
{
    public static void CreatePullRequestIfNonExists(string targetRepositoryOwner, string targetRepositoryName,
        string currentBranch, string gitHubAccessToken)
    {
        CreatePullRequestIfNonExistsAsync(targetRepositoryOwner, targetRepositoryName, currentBranch, gitHubAccessToken)
            .Wait();
    }

    static async Task CreatePullRequestIfNonExistsAsync(string targetRepositoryOwner,
        string targetRepositoryName, string currentBranch, string gitHubAccessToken)
    {
        const string prMessage = "Update References";
        var client = new GitHubClient(new ProductHeaderValue("nuke.build"),
            new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));

        try
        {
            var repository = await client.Repository.Get(targetRepositoryOwner, targetRepositoryName);

            var repositoryId = repository.Id;
            var pullRequests = await client.PullRequest.GetAllForRepository(repositoryId);
            if (!pullRequests.Any(x =>
                x.State == ItemState.Open && !x.Merged && x.Head.Label == $"{targetRepositoryOwner}:{currentBranch}" &&
                x.Title == prMessage && x.Base.Label == $"{targetRepositoryOwner}:master"))
                await client.PullRequest.Create(repositoryId,
                    new NewPullRequest(prMessage, $"{targetRepositoryOwner}:{currentBranch}", "master"));
        }
        catch (Exception e)
        {
            ControlFlow.Fail($"Could not find Repository: {e.Message}");
        }
    }
}