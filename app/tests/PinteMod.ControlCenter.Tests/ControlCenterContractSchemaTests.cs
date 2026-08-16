using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class ControlCenterContractSchemaTests
{
    [TestMethod]
    public void BundledSchemas_AreClosedAndExposeOnlyValidatedActions()
    {
        var capabilities = Load("control_center_capabilities.schema.json");
        var feedback = Load("action_feedback.schema.json");
        var transition = Load("map_transition.schema.json");
        var identity = Load("server_identity.schema.json");

        Assert.IsFalse(capabilities.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.IsFalse(
            capabilities.RootElement.GetProperty("properties").GetProperty("change_map").GetProperty("const").GetBoolean());
        Assert.AreEqual(
            "boolean",
            capabilities.RootElement.GetProperty("properties").GetProperty("set_join_password").GetProperty("type").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "restart_map", "spawn_boss", "set_hostname", "set_join_password", "clear_join_password" },
            feedback.RootElement.GetProperty("properties").GetProperty("action").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.IsFalse(feedback.RootElement.GetRawText().Contains("trigger_event", StringComparison.Ordinal));
        StringAssert.Contains(feedback.RootElement.GetRawText(), "set_join_password");
        Assert.AreEqual(
            "restart_map",
            transition.RootElement.GetProperty("properties").GetProperty("action").GetProperty("const").GetString());
        Assert.AreEqual(
            "integer",
            identity.RootElement.GetProperty("properties").GetProperty("revision").GetProperty("type").GetString());
        Assert.AreEqual(
            1L,
            identity.RootElement.GetProperty("properties").GetProperty("revision").GetProperty("minimum").GetInt64());
        Assert.IsFalse(identity.RootElement.GetProperty("properties").GetProperty("revision").TryGetProperty("const", out _));
    }

    private static JsonDocument Load(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "contracts",
            "control-center",
            "v1",
            fileName);
        Assert.IsTrue(File.Exists(path), $"Schéma absent de la sortie de test : {fileName}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
