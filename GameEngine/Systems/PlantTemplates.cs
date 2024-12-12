using WebPeli.GameEngine.World.WorldData;

namespace WebPeli.GameEngine.Systems;

public static class PlantTemplates 
{
    // Example template for grass (ground cover)
    public static readonly PlantRequirements Grass = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };

    public static readonly PlantRequirements Weed = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };

    public static readonly PlantRequirements Flower = new()
    {
        IsGroundCover = true,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 0,  // Ground cover
        MaxWidth = 0,   // Ground cover
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 2,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = 0,
                    MaxTemperature = 40,
                    MinMoisture = 10,
                    MaxMoisture = 90,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define grass yields
    };
    // Example template for an oak tree (physical plant)
    public static readonly PlantRequirements OakTree = new()
    {
        IsGroundCover = false,
        SuitableGrowthTiles = [TileMaterial.Dirt, TileMaterial.Mud],
        MaxHeight = 5,  // Will affect rendering
        MaxWidth = 2,   // 2x2 tiles at full size
        GrowthStages = new()
        {
            { PlantMaturityStatus.Seed, new GrowthRequirements 
                {
                    MinDaysInStage = 5,
                    MinTemperature = 5,
                    MaxTemperature = 35,
                    MinMoisture = 30,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring]
                }
            },
            { PlantMaturityStatus.Seedling, new GrowthRequirements 
                {
                    MinDaysInStage = 15,
                    MinTemperature = 0,
                    MaxTemperature = 35,
                    MinMoisture = 20,
                    MaxMoisture = 80,
                    ValidSeasons = [Season.Spring, Season.Summer]
                }
            },
            { PlantMaturityStatus.Sapling, new GrowthRequirements 
                {
                    MinDaysInStage = 30,
                    MinTemperature = -5,
                    MaxTemperature = 40,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Young, new GrowthRequirements 
                {
                    MinDaysInStage = 60,
                    MinTemperature = -10,
                    MaxTemperature = 40,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn]
                }
            },
            { PlantMaturityStatus.Mature, new GrowthRequirements 
                {
                    MinDaysInStage = 0,  // No limit
                    MinTemperature = -20,
                    MaxTemperature = 45,
                    MinMoisture = 20,
                    MaxMoisture = 70,
                    ValidSeasons = [Season.Spring, Season.Summer, Season.Autumn, Season.Winter]
                }
            }
        },
        HarvestYields = new HarvestTable() // Define oak tree yields
    };

    // Method to create health component only for physical plants
    public static HealthComponent? CreateHealthComponent(PlantRequirements template, PlantMaturityStatus status)
    {
        if (template.IsGroundCover)
            return null;

        var baseHealth = template.MaxWidth * template.MaxWidth * 50; // Health based on size
        
        var healthMultiplier = status switch
        {
            PlantMaturityStatus.Seed => 0.2f,
            PlantMaturityStatus.Seedling => 0.4f,
            PlantMaturityStatus.Sapling => 0.6f,
            PlantMaturityStatus.Young => 0.8f,
            PlantMaturityStatus.Mature => 1.0f,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };

        return new HealthComponent
        {
            Health = (int)(baseHealth * healthMultiplier),
            MaxHealth = (int)(baseHealth * healthMultiplier),
            RegenRate = template.MaxWidth // Bigger plants regenerate faster
        };
    }
}
