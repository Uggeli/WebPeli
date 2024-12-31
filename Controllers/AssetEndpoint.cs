using WebPeli.Network;
using WebPeli.GameEngine;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System;
using System.Text.RegularExpressions;

namespace WebPeli.Controllers;

public static partial class StringValidator
{
    private const string VALID_FILENAME_REGEX = @"^[a-zA-Z0-9\-_\.]+$";
    [GeneratedRegex(VALID_FILENAME_REGEX)]
    public static partial Regex Validate();
}


public class AssetEndpoint(AssetManager assetManager, ILogger<AssetEndpoint> logger) : ControllerBase
{
    private readonly AssetManager _assetManager = assetManager;
    private readonly ILogger<AssetEndpoint> _logger = logger;
    private const int MAX_ASSET_SIZE = 100 * 1024 * 1024; // 100MB
    private const string VALID_FILENAME_REGEX = @"^[a-zA-Z0-9\-_\.]+$";

    [HttpGet("api/asset/{type}/{name}")]
    public IActionResult GetAsset(AssetType type, string name)
    {
        var asset = _assetManager.GetAsset(type, name);
        if (asset == null)
        {
            return NotFound();
        }
        var (data, metadata) = asset.Value;
        return Ok(new { data, metadata });
    }

    // Delete routes
    [HttpDelete("api/asset/{type}/{name}")]
    public IActionResult DeleteAsset(AssetType type, string name)
    {
        _assetManager.DeleteAsset(type, name);
        return Ok();
    }

    [HttpDelete("api/asset/{type}")]
    public IActionResult DeleteAllAssets(AssetType type)
    {
        _assetManager.DeleteAllAssets(type);
        return Ok();
    }

    [HttpDelete("api/asset")]
    public IActionResult DeleteAllAssets()
    {
        _assetManager.DeleteAllAssets();
        return Ok();
    }

    // List routes
    [HttpGet("api/asset/list")]
    public IEnumerable<string> ListAssets()
    {
        return _assetManager.ListAssets();
    }

    [HttpGet("api/asset/list/{type}")]
    public IEnumerable<string> ListAssets(AssetType type)
    {
        return _assetManager.ListAssets(type);
    }

    // Cache routes
    [HttpDelete("api/asset/cache")]
    public IActionResult ClearCache()
    {
        _assetManager.ClearCache();
        return Ok();
    }

    [HttpDelete("api/asset/cache/{type}")]
    public IActionResult ClearCache(AssetType type)
    {
        _assetManager.ClearCache(type);
        return Ok();
    }

    [HttpDelete("api/asset/cache/{type}/{name}")]
    public IActionResult ClearCache(AssetType type, string name)
    {
        _assetManager.ClearCache(type, name);
        return Ok();
    }

    // Save routes
    [HttpPost("api/asset/{type}/{name}")]
    public async Task<IActionResult> SaveAsset(AssetType type, string name, [FromBody] SaveAssetRequest request)
    {
        byte[] dataBytes = Convert.FromBase64String(request.Data);

        if (dataBytes.Length > MAX_ASSET_SIZE)
            return BadRequest($"Asset size exceeds maximum of {MAX_ASSET_SIZE / 1024 / 1024}MB");
            
        if (!StringValidator.Validate().IsMatch(name))
            return BadRequest("Invalid asset name. Use only letters, numbers, dots, dashes and underscores");

        try 
        {
            if (await _assetManager.SaveAssetAsync(type, name, request.Metadata, dataBytes))
                return Ok();
            return Conflict("Asset already exists");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save asset {type}/{name}", type, name);
            return StatusCode(500, "Failed to save asset");
        }
    }

    // Add batch operations
    [HttpPost("api/assets/batch")]
    public async Task<IActionResult> BatchSaveAssets([FromBody] List<AssetUploadDto> assets)
    {
        if (assets.Count > 100)
            return BadRequest("Maximum 100 assets per batch");

        var results = new List<AssetUploadResult>();
        
        foreach (var asset in assets)
        {
            try
            {
                var success = await _assetManager.SaveAssetAsync(asset.Type, asset.Name, asset.Metadata, asset.Data);
                results.Add(new AssetUploadResult(asset.Name, success));
            }
            catch
            {
                results.Add(new AssetUploadResult(asset.Name, false));
            }
        }

        return Ok(results);
    }
}

public class SaveAssetRequest
{
    public required string Data { get; set; }
    public string? Metadata { get; set; }
}


public record AssetUploadDto(AssetType Type, string Name, string? Metadata, byte[] Data);
public record AssetUploadResult(string Name, bool Success);


public enum AssetType
{
    None = 0,
    Image,
    Sound,
    Font,
    Model
}

public class AssetManager
{
    private static readonly string _assetPath = Config.assetPath;
    private readonly ILogger<AssetManager> _logger;
    private readonly ConcurrentDictionary<string, WeakReference<byte[]>> _cache;
    public AssetManager(ILogger<AssetManager> logger)
    {
        _logger = logger;
        _cache = new ConcurrentDictionary<string, WeakReference<byte[]>>();

        // Check if asset folder exists
        if (!Directory.Exists(_assetPath))
        {
            Directory.CreateDirectory(_assetPath);
        }
        // Check if subfolders exist
        foreach (AssetType type in Enum.GetValues<AssetType>())
        {
            if (type == AssetType.None) continue;
            string subFolder = _resolveSubFolder(type);
            if (!Directory.Exists(Path.Combine(_assetPath, subFolder)))
            {
                Directory.CreateDirectory(Path.Combine(_assetPath, subFolder));
            }
        }
    }

    public bool SaveAsset(AssetType type, string name, string? metadata,byte[] data)
    {
        if (_checkIfAssetExists(name, type))
        {
            return false;
        }
        string subFolder = _resolveSubFolder(type);
        File.WriteAllBytes(Path.Combine(_assetPath, subFolder, name), data);
        if (metadata != null)
        {
            File.WriteAllText(Path.Combine(_assetPath, subFolder, name + ".meta"), metadata);
        }
        return true;
    }

    private bool _checkIfAssetExists(string name, AssetType type = AssetType.None)
    {
        string subFolder = _resolveSubFolder(type);
        return File.Exists(Path.Combine(_assetPath, subFolder, name));
    }

    private string _resolveSubFolder(AssetType type)
    {
        return type switch
        {
            AssetType.Image => "Images",
            AssetType.Sound => "Sounds",
            AssetType.Font => "Fonts",
            AssetType.Model => "Models",
            _ => "",
        };
    }

    public (byte[] asset, string? metadata)? GetAsset(AssetType type, string name)
    {
        // Check cache
        var cacheKey = $"{type}:{name}";
        try 
        {
            if (_cache.TryGetValue(cacheKey, out var weakRef) && 
                weakRef.TryGetTarget(out var cachedData))
            {
                return (cachedData, null);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get asset from cache");
        }

        // Load from disk
        if (!_checkIfAssetExists(name, type))
        {
            return null;
        }
        try
        {
            string subFolder = _resolveSubFolder(type);
            byte[] asset = File.ReadAllBytes(Path.Combine(_assetPath, subFolder, name));
            string? metadata = null;
            if (File.Exists(Path.Combine(_assetPath, subFolder, name + ".meta")))
            {
                metadata = File.ReadAllText(Path.Combine(_assetPath, subFolder, name + ".meta"));
            }
            // Cache
            _cache[cacheKey] = new WeakReference<byte[]>(asset);
            return (asset, metadata);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load asset from disk");
            return null;
        }
    }

    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Asset cache cleared");
    }

    public void ClearCache(AssetType type, string name)
    {
        _cache.TryRemove($"{type}:{name}", out _);
        _logger.LogInformation($"Asset cache cleared for {type}:{name}");
    }

    public void ClearCache(AssetType type)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith($"{type}:"))
            {
                _cache.TryRemove(key, out _);
            }
        }
        _logger.LogInformation($"Asset cache cleared for {type}");
    }

    public IEnumerable<string> ListAssets(AssetType type)
    {
        var path = Path.Combine(_assetPath, _resolveSubFolder(type));
        if (!Directory.Exists(path))
        {
            return [];
        }
        return Directory.GetFiles(path).Select(f => Path.GetFileName(f)).Where(f => !f.EndsWith(".meta"));
    }

    public IEnumerable<string> ListAssets()
    {
        var assets = new List<string>();
        foreach (AssetType type in Enum.GetValues<AssetType>())
        {
            if (type == AssetType.None) continue;
            assets.AddRange(ListAssets(type));
        }
        return assets;
    }

    public void DeleteAsset(AssetType type, string name)
    {
        string subFolder = _resolveSubFolder(type);
        File.Delete(Path.Combine(_assetPath, subFolder, name));
        if (File.Exists(Path.Combine(_assetPath, subFolder, name + ".meta")))
        {
            File.Delete(Path.Combine(_assetPath, subFolder, name + ".meta"));
        }
        ClearCache(type, name);
    }

    public void DeleteAllAssets()
    {
        foreach (AssetType type in Enum.GetValues<AssetType>())
        {
            if (type == AssetType.None) continue;
            foreach (var asset in ListAssets(type))
            {
                DeleteAsset(type, asset);
            }
        }
    }

    public void DeleteAllAssets(AssetType type)
    {
        foreach (var asset in ListAssets(type))
        {
            DeleteAsset(type, asset);
        }
    }

    public async Task<bool> SaveAssetAsync(AssetType type, string name, string? metadata, byte[] data)
    {
        if (_checkIfAssetExists(name, type))
            return false;

        string subFolder = _resolveSubFolder(type);
        string filePath = Path.Combine(_assetPath, subFolder, name);
        
        await using var fs = new FileStream(filePath, FileMode.Create);
        await fs.WriteAsync(data);

        if (metadata != null)
        {
            await File.WriteAllTextAsync(filePath + ".meta", metadata);
        }

        return true;
    }

    public async Task<(byte[] asset, string? metadata)?> GetAssetAsync(AssetType type, string name)
    {
        // Check cache first
        var result = GetAsset(type, name);
        if (result.HasValue)
            return result;

        // Load from disk asynchronously
        try
        {
            string subFolder = _resolveSubFolder(type);
            string filePath = Path.Combine(_assetPath, subFolder, name);
            
            byte[] asset = await File.ReadAllBytesAsync(filePath);
            string? metadata = null;
            
            if (File.Exists(filePath + ".meta"))
                metadata = await File.ReadAllTextAsync(filePath + ".meta");

            // Cache the result
            _cache[$"{type}:{name}"] = new WeakReference<byte[]>(asset);
            
            return (asset, metadata);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load asset asynchronously");
            return null;
        }
    }

}