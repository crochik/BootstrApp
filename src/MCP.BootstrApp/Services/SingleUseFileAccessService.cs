using System.Text;
using PI.Shared.Models;

namespace MCP.Services;

public class SingleUseFileAccessService
{
    private readonly Dictionary<Guid, CachedItem> _cache = new();
    
    public async Task<Result<Guid>> AddAsync(IEntityContext context, string content, string contentType)
    {
        var id = Guid.NewGuid();
        _cache[id] = new CachedItem
        {
            Content = content, // Encoding.UTF8.GetBytes(content),
            ContentType = contentType,
        };

        return Result.Success(id);
    }

    public async Task<Result<CachedItem>> GetAsync(Guid id)
    {
        await Task.CompletedTask;
        
        if (_cache.Remove(id, out var item))
        {
            return Result.Success(item);
        }

        return Result.Error<CachedItem>("Invalid or expired Link");
    }

    public class CachedItem
    {
        public string Content { get; set; }
        public string ContentType { get; set; }
        public string Name { get; set; }
    }
}