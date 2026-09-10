using BitwardenStreamdeckPlugin.Models;
using Newtonsoft.Json;

namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// Covers how the plugin reads the JSON that the Bitwarden CLI prints. These are the
/// shapes that break when the CLI changes its output.
/// </summary>
public class ItemParsingTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine("Fixtures", name));

    [Fact]
    public void ParseItem_reads_credentials_from_the_login_section()
    {
        Item item = Get.ParseItem(Fixture("get-item.json"));

        Assert.Equal("octocat", item.UserName);
        Assert.Equal("correct horse battery staple", item.Password);
        // The seed, not a usable code - the Get action fetches the code separately.
        Assert.Equal("JBSWY3DPEHPK3PXP", item.TotpSecret);
    }

    [Fact]
    public void ParseItem_leaves_name_and_id_unset_because_they_live_outside_the_login_section()
    {
        // Documents existing behaviour rather than endorsing it: the item is deserialized
        // from the nested "login" object, so the entry's own name and id never arrive.
        Item item = Get.ParseItem(Fixture("get-item.json"));

        Assert.Null(item.Name);
        Assert.Equal(Guid.Empty, item.Id);
    }

    [Fact]
    public void ParseItem_rejects_an_entry_without_a_login_section()
    {
        // Secure notes and cards have no credentials to type.
        string secureNote = """{ "object": "item", "name": "Secure Note", "type": 2 }""";

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => Get.ParseItem(secureNote));

        Assert.Contains("login", error.Message);
    }

    [Fact]
    public void ParseItem_rejects_output_that_is_not_json()
    {
        // A locked vault makes the CLI print a plain message instead of JSON.
        Assert.ThrowsAny<JsonException>(() => Get.ParseItem("You are not logged in."));
    }

    [Fact]
    public void ParseItem_tolerates_a_missing_totp()
    {
        string withoutTotp = """
        { "object": "item", "name": "X", "login": { "username": "u", "password": "p" } }
        """;

        Item item = Get.ParseItem(withoutTotp);

        Assert.Equal("u", item.UserName);
        Assert.Null(item.TotpSecret);
    }

    [Fact]
    public void List_items_output_maps_onto_the_item_list_dto()
    {
        List<ItemListDto>? items =
            JsonConvert.DeserializeObject<List<ItemListDto>>(Fixture("list-items.json"));

        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);
        Assert.Equal("GitHub", items[0].ItemName);
        Assert.Equal(Guid.Parse("8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"), items[0].ItemId);
        Assert.Equal("Example Mail", items[1].ItemName);
    }

    [Fact]
    public void List_items_includes_entries_that_have_no_login_section()
    {
        // The property inspector lists every vault entry, including secure notes.
        List<ItemListDto> items =
            JsonConvert.DeserializeObject<List<ItemListDto>>(Fixture("list-items.json"))!;

        Assert.Contains(items, i => i.ItemName == "Secure Note");
    }

    [Fact]
    public void The_picker_labels_each_entry_with_its_username()
    {
        // Several logins for the same site are indistinguishable by name alone.
        List<ItemListDto> items = Get.ParseItemList(Fixture("list-items.json"));

        Assert.Equal("GitHub (octocat)", items[0].ItemName);
        Assert.Equal("Example Mail (user@example.com)", items[1].ItemName);
    }

    [Fact]
    public void An_entry_without_a_username_keeps_its_plain_name()
    {
        List<ItemListDto> items = Get.ParseItemList(Fixture("list-items.json"));

        Assert.Equal("Secure Note", items[2].ItemName);
    }

    [Fact]
    public void An_entry_with_an_empty_username_gets_no_empty_parentheses()
    {
        string json = """
        [{ "object": "item", "id": "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f",
           "name": "No User", "type": 1, "login": { "username": "", "password": "p" } }]
        """;

        Assert.Equal("No User", Get.ParseItemList(json)[0].ItemName);
    }

    [Fact]
    public void The_picker_keeps_the_item_ids_intact()
    {
        // The id is what gets stored as the selection, so relabelling must not disturb it.
        List<ItemListDto> items = Get.ParseItemList(Fixture("list-items.json"));

        Assert.Equal(Guid.Parse("8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"), items[0].ItemId);
    }

    [Fact]
    public void No_credentials_survive_into_the_saved_settings()
    {
        // 'bw list items' hands back whole entries, passwords and TOTP seeds included.
        // That list is written into the Stream Deck's settings, so it must carry none.
        List<ItemListDto> items = Get.ParseItemList(Fixture("list-items.json"));

        string persisted = JsonConvert.SerializeObject(items);

        Assert.DoesNotContain("correct horse battery staple", persisted);
        Assert.DoesNotContain("hunter2", persisted);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", persisted);
        Assert.DoesNotContain("password", persisted, StringComparison.OrdinalIgnoreCase);
    }
}
