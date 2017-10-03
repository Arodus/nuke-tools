using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common.Git;
using Nuke.Core;
using Octokit;
using Octokit.Internal;

public static class EX
{
    public static void Deconstruct<T>(this T[] items, out T t0)
    {
        t0 = items.Length > 0 ? items[0] : default(T);
    }

    public static void Deconstruct<T>(this T[] items, out T t0, out T t1)
    {
        t0 = items.Length > 0 ? items[0] : default(T);
        t1 = items.Length > 1 ? items[1] : default(T);
    }
}

public static class ReferencePullRequest
{
    const string PullRequestMessage = "Update References";

    public static void CreatePullRequestIfNeeded(GitRepository repository, string accessToken)
    {
        var client = CreateClient(accessToken);
        var (owner, name) = repository.Identifier.Split('/');
        var pullRequests = client.PullRequest.GetAllForRepository(owner, name).Result;
        var hasPullRequest = pullRequests.Any(x => x.State == ItemState.Open);
        return;

        var pullRequest = new NewPullRequest(PullRequestMessage, $"{owner}:{repository.Branch}", "master");
        client.PullRequest.Create(owner, name, pullRequest);
    }

    static GitHubClient CreateClient(string gitHubAccessToken)
    {
        return new GitHubClient(
            new ProductHeaderValue("nuke.build"),
            new InMemoryCredentialStore(new Credentials(gitHubAccessToken)));
    }
}