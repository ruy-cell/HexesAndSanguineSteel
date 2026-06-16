using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace HexesAndSanguineSteel;

internal static class InventoryCostUtility
{
    static readonly string[] RemoveMethodNames =
    [
        "TryRemoveInventoryItem",
        "RemoveInventoryItem",
        "TryRemoveItem",
        "RemoveItem",
        "ConsumeInventoryItem",
        "TryConsumeInventoryItem"
    ];

    internal static bool TryConsumeUpgradeCost(PlayerLookupResult player, IReadOnlyList<CustomWeaponCostDef> costs, out string message)
    {
        message = string.Empty;

        if (costs.Count == 0)
        {
            message = "No extra upgrade cost configured.";
            return true;
        }

        if (!InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, player.CharacterEntity, out Entity inventoryEntity))
        {
            message = $"Could not resolve inventory entity for '{player.Name}'.";
            return false;
        }

        var consumed = new List<string>();

        foreach (var cost in costs)
        {
            if (!cost.Item.HasValue() || cost.Amount <= 0)
                continue;

            if (!TryRemoveItem(inventoryEntity, cost.Item, cost.Amount, out string removeMessage))
            {
                string alreadyConsumed = consumed.Count == 0
                    ? string.Empty
                    : $" Already consumed before failure: {string.Join(", ", consumed)}.";

                message = $"Missing or could not consume upgrade cost {cost.Amount}x {cost.Name} ({cost.Item.GuidHash}). {removeMessage}{alreadyConsumed}";
                return false;
            }

            consumed.Add($"{cost.Amount}x {cost.Name}");
        }

        message = consumed.Count == 0
            ? "No valid extra upgrade cost configured."
            : $"Consumed upgrade cost: {string.Join(", ", consumed)}.";

        return true;
    }

    static bool TryRemoveItem(Entity inventoryEntity, PrefabGUID item, int amount, out string message)
    {
        message = string.Empty;

        // Preferred path: ServerGameManager instance methods.
        if (TryInvokeRemoveMethod(Core.ServerGameManager, Core.ServerGameManager.GetType(), inventoryEntity, item, amount, out bool success, out message))
            return success;

        // Fallback: static inventory utility methods from the ProjectM assembly.
        Assembly projectM = typeof(InventoryUtilities).Assembly;
        foreach (var typeName in new[]
                 {
                     "ProjectM.InventoryUtilities",
                     "ProjectM.InventoryUtilitiesServer",
                     "ProjectM.InventoryUtilities_Server"
                 })
        {
            Type? type = projectM.GetType(typeName);
            if (type is null)
                continue;

            if (TryInvokeRemoveMethod(null, type, inventoryEntity, item, amount, out success, out message))
                return success;
        }

        message = "No compatible inventory item removal method was found in this VRA build.";
        return false;
    }

    static bool TryInvokeRemoveMethod(object? instance, Type type, Entity inventoryEntity, PrefabGUID item, int amount, out bool success, out string message)
    {
        success = false;
        message = string.Empty;

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | (instance is null ? BindingFlags.Static : BindingFlags.Instance);

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (!RemoveMethodNames.Any(name => string.Equals(method.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (!TryBuildArguments(method, inventoryEntity, item, amount, out object?[] args))
                continue;

            try
            {
                object? result = method.Invoke(instance, args);
                success = InterpretResult(result);
                message = success ? $"Removed {amount}x {item.GuidHash} using {type.Name}.{method.Name}." : $"{type.Name}.{method.Name} returned failure.";
                return true;
            }
            catch (TargetInvocationException ex)
            {
                message = $"{type.Name}.{method.Name} failed: {ex.InnerException?.Message ?? ex.Message}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"{type.Name}.{method.Name} failed: {ex.Message}";
                return true;
            }
        }

        return false;
    }

    static bool TryBuildArguments(MethodInfo method, Entity inventoryEntity, PrefabGUID item, int amount, out object?[] args)
    {
        var parameters = method.GetParameters();
        args = new object?[parameters.Length];

        bool usedEntity = false;
        bool usedGuid = false;
        bool usedAmount = false;

        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;

            if (parameterType == typeof(EntityManager))
            {
                args[i] = Core.EntityManager;
            }
            else if (parameterType == typeof(Entity))
            {
                args[i] = inventoryEntity;
                usedEntity = true;
            }
            else if (parameterType == typeof(PrefabGUID))
            {
                args[i] = item;
                usedGuid = true;
            }
            else if (parameterType == typeof(int))
            {
                args[i] = amount;
                usedAmount = true;
            }
            else if (parameterType == typeof(bool))
            {
                // Most inventory remove helpers use extra bools for logging/allow-partial/internal flags.
                // False is the least invasive default.
                args[i] = false;
            }
            else
            {
                return false;
            }
        }

        return usedEntity && usedGuid && usedAmount;
    }

    static bool InterpretResult(object? result)
    {
        if (result is null)
            return true;

        if (result is bool b)
            return b;

        Type resultType = result.GetType();

        PropertyInfo? successProperty = resultType.GetProperty("Success", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (successProperty is not null && successProperty.PropertyType == typeof(bool))
            return (bool)(successProperty.GetValue(result) ?? false);

        FieldInfo? successField = resultType.GetField("Success", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (successField is not null && successField.FieldType == typeof(bool))
            return (bool)(successField.GetValue(result) ?? false);

        return true;
    }
}
