namespace Kaizen.BlogEngine;

public interface IPostService
{
    /// <summary>Published posts, newest first.</summary>
    IReadOnlyList<Post> GetAll();
    Post? GetBySlug(string slug);
    IReadOnlyList<Post> GetByTag(string tag);
    IReadOnlyList<string> GetTags();
    string UrlPrefix { get; }
}
