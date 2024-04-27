using BlazorApp1.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace PrettyPlanLib
{
    public partial class Plan
    {
        List<TerraformAction> actions = new List<TerraformAction>();
        bool fancyView = false;
        //string filename = @"C:\Users\CarlintVeld\OneDrive - CloudNation\Customers\Assai\Projects\2023-09 Enforces replacement json terraform\terraform.tfplan public endpoint replacement.json";
        //string filename = @"C:\Users\CarlintVeld\OneDrive - CloudNation\Customers\Assai\Projects\2023-09 Enforces replacement json terraform\appreg-changes.json";
        string filename;
        string textarea = string.Empty;
        InputLargeTextArea? TextArea;
        string error;

        protected async override Task OnInitializedAsync()
        {
            //Load();
        }

        async Task LoadTextarea()
        {
            error = string.Empty;
            try
            {
                var streamReader = await TextArea!.GetTextAsync(maxLength: 4_000_000);
                var getTextResult = await streamReader.ReadToEndAsync();
                ParseTerraform(getTextResult);
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            finally { StateHasChanged(); }
        }

        protected void Load()
        {
            string text = File.ReadAllText(filename);
            ParseTerraform(text);
        }

        protected void ParseTerraform(string json)
        {
            actions = new List<TerraformAction>();
            var obj = JsonObject.Parse(json);
            var arr = obj?["resource_changes"];
            if (arr != null)
            {
                foreach (var elem in arr.AsArray())
                {
                    JsonObject item = elem?.AsObject() ?? throw new ArgumentException("JsonNode should be a JsonObject");
                    var replacePaths = ParseReplacePaths(item?["change"]?["replace_paths"]?.AsArray() ?? []);
                    var address = item?["address"]?.ToString() ?? throw new ArgumentNullException("address");
                    if (address == "module.Infrastructure.azuread_application.consumers[\"enexis\"]")
                    {

                    }
                    var action = new TerraformAction {
                        Address = address,
                        ChangeType = ParseChangeType(item!["change"]?["actions"]?.AsArray() ?? []),
                        ActionReason = item?["action_reason"]?.AsValue().ToString() ?? "<not specified>",
                        Changes = ParseChanges(item?["change"]?.AsObject() ?? new JsonObject(), replacePaths),
                        ReplacePaths = replacePaths,
                        TerraformResourceId = ParseId(item!)
                    };
                    if (action.ChangeType != ChangeType.NoOperation) { 
                        actions.Add(action); 
                    }
                }
            }
        }



        private void accordion(TerraformAction action)
        {
            action.IsOpen = !action.IsOpen;
        }
      
        public Dictionary<string, bool> ParseReplacePaths(JsonArray replacePaths)
        {
            var result = new Dictionary<string, bool>();
            var list = replacePaths.SelectMany(c => c?.AsArray() ?? new JsonArray()).Select(c => c?.ToString() ?? string.Empty)?.ToList() ?? new List<string>();
            foreach (var item in list)
            {
                result.Add(item, true);
            }
            return result;
        }

        private TerraformResourceId ParseId(JsonObject resource)
        {
            
            var name = resource["name"]?.ToString() ?? 
                throw new ArgumentNullException("name");
            var address = resource["address"]?.ToString() ?? 
                throw new ArgumentNullException("address");
            //var addressWithoutResourceName = address.Substring(0, address.Length - name.Length - 2);
            var module_address = resource["module_address"]?.ToString() ?? string.Empty; // if change is in root module, module_address is omitted
            var idSegments = module_address.Split('.');
            var resourcePrefixes = idSegments.ToList();

            return new TerraformResourceId
            {
                Name = name,
                Type = resource["type"]?.ToString() ?? throw new ArgumentNullException("type"),
                Address = resource["address"]?.ToString() ?? throw new ArgumentNullException("address"),
                Index = resource["index"]?.ToString(),
                Prefixes = resourcePrefixes,
            };
        }

        public ChangeType ParseChangeType(JsonArray changes)
        {
            var arr = changes.Select(c => c.ToString());
            if (arr.Contains("no-op")) return ChangeType.NoOperation;
            if (arr.Contains("read")) return ChangeType.Read;
            if (arr.Contains("update")) return ChangeType.Update;

            if (arr.Contains("create") && arr.Contains("delete")) return ChangeType.Recreate;

            if (arr.Contains("create")) return ChangeType.Create;
            if (arr.Contains("delete")) return ChangeType.Destroy;

            return ChangeType.Unknown;
        }


        public List<TerraformChange> ParseChanges_original(JsonObject changes, Dictionary<string, bool> replacePaths)
        {
            var changesBefore = changes?["before"]?.AsObject() ?? new JsonObject();
            var changesAfter = changes?["after"]?.AsObject() ?? new JsonObject();
            var changesAfterUnknown = changes?["after_unknown"]?.AsObject() ?? new JsonObject();
            var propsBefore = changesBefore.Select(c => c.Key) ?? Array.Empty<string>();
            var propsAfter = changesAfter.Select(c => c.Key) ?? Array.Empty<string>();
            var propsAfterUnknown = changesAfterUnknown.Select(c => c.Key) ?? Array.Empty<string>();

            var propsAllObj = propsBefore.Concat(propsAfter).Concat(propsAfterUnknown)
                .Distinct()
                .ToDictionary(
                    property => property,
                    property => new TerraformChange { Property = property, Old = null, New = null }
                );

            foreach (var property in propsAllObj.Values)
            {
                if (changesBefore.ContainsKey(property.Property))
                {
                    property.Old = changesBefore[property.Property];
                    property.OldHasValue = true;
                }
                if (changesAfterUnknown.ContainsKey(property.Property))
                {
                    property.NewHasValue = true;
                    property.NewComputed = true;
                }
                if (changesAfter.ContainsKey(property.Property))
                {
                    property.NewHasValue = true;
                    property.New = changesAfter[property.Property];
                    if (property.NewComputed)
                    {
                        // throw new Exception("Changed and computed???");
                    }
                }
                property.CausesReplacement = replacePaths.ContainsKey(property.Property);
            }

            var props = propsAllObj.Values
                .Where(property => property.NewComputed || property.OldHasValue != property.NewHasValue || !JsonNode.DeepEquals(property.Old, property.New))
                .ToList();

            return props;
        }



        public List<TerraformChange> ParseChanges(JsonObject changes, Dictionary<string, bool> replacePaths)
        {
            var changesBefore = changes?["before"]?.AsObject() ?? new JsonObject();
            var changesAfter = changes?["after"]?.AsObject() ?? new JsonObject();
            var changesAfterUnknown = changes?["after_unknown"]?.AsObject() ?? new JsonObject();
            var propsBefore = changesBefore.Select(c => c.Key) ?? Array.Empty<string>();
            var propsAfter = changesAfter.Select(c => c.Key) ?? Array.Empty<string>();
            var propsAfterUnknown = changesAfterUnknown.Select(c => c.Key) ?? Array.Empty<string>();

            var propsAllObj = propsBefore.Concat(propsAfter).Concat(propsAfterUnknown)
                .Distinct()
                .ToDictionary(
                    property => property,
                    property => new TerraformChange { Property = property, Old = null, New = null }
                );

            foreach (var property in propsAllObj.Values)
            {
                if (changesBefore.ContainsKey(property.Property))
                {
                    property.Old = changesBefore[property.Property];
                    property.OldHasValue = true;
                }
                if (changesAfterUnknown.ContainsKey(property.Property))
                {
                    property.NewHasValue = true;
                    property.NewComputed = true;
                }
                if (changesAfter.ContainsKey(property.Property))
                {
                    property.NewHasValue = true;
                    property.New = changesAfter[property.Property];
                    if (property.NewComputed)
                    {
                        // throw new Exception("Changed and computed???");
                    }
                }
                property.CausesReplacement = replacePaths.ContainsKey(property.Property);
                if (property.Property == "timeouts")
                {

                }
                var result = JsonValueTraverse(changesAfter.ContainsKey(property.Property), changesAfter[property.Property], changesAfterUnknown[property.Property], string.Empty, arrayValueIndent: false);
                property.NewSerialized = result.StringBuilder.ToString();
                property.NewComputed = result.Computed;
            }

            var props = propsAllObj.Values
                .Where(property => property.NewComputed || property.OldHasValue != property.NewHasValue || !JsonNode.DeepEquals(property.Old, property.New))
                .ToList();

            return props;
        }

        JsonTraverseReturnValue JsonValueTraverse(bool leftHasKey, JsonNode? left, JsonNode? right, string indent, bool arrayValueIndent)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool computed = false;
            if (!leftHasKey)
            {
                stringBuilder.Append("<i>(computed)</i>");
                computed = true;
            }
            else
            {
                switch (left)
                {
                    case null:
                        if (arrayValueIndent)
                        {
                            stringBuilder.Append($"{indent}");
                        }
                        stringBuilder.Append("null");
                        if (right?.AsValue().GetValue<bool>() ?? false)
                        {
                            stringBuilder.Append(" <i>(computed)</i>");
                            computed = true;
                        }
                        break;
                    case JsonObject o:
                        var objResult = JsonObjectTraverse(o, right?.AsObject(), indent);
                        stringBuilder.Append(objResult.StringBuilder);
                        computed |= objResult.Computed;
                        break;
                    case JsonValue v:
                        if (arrayValueIndent)
                        {
                            stringBuilder.Append($"{indent}");
                        }
                        switch (v.GetValueKind())
                        {
                            case JsonValueKind.String:
                                stringBuilder.Append($"\"{v.GetValue<string>()}\"");
                                break;
                            default:
                                stringBuilder.Append($"{v}");
                                break;
                        }
                        if (right?.AsValue().GetValue<bool>() ?? false)
                        {
                            stringBuilder.Append(" <i>(computed)</i>");
                            computed = true;
                        }
                        break;
                    case JsonArray a:
                        var arrResult = JsonArrayTraverse(a, right?.AsArray(), indent);
                        stringBuilder.Append(arrResult.StringBuilder);
                        computed |= arrResult.Computed;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(left));
                }
            }
            return new JsonTraverseReturnValue { 
                StringBuilder = stringBuilder, 
                Computed = computed 
            };
        }

        JsonTraverseReturnValue JsonObjectTraverse(JsonObject left, JsonObject? right, string indent)
        {
            List<string> list = [];
            var stringBuilder = new StringBuilder();            
            bool computed = false;
            stringBuilder.Append($"{indent}{{");
            var merge = new Dictionary<string, MergeJsonNode>();
            foreach (var kv in left)
            {
                if (!merge.ContainsKey(kv.Key))
                {
                    merge.Add(kv.Key, new MergeJsonNode());
                }
                merge[kv.Key].Left = kv.Value;
                merge[kv.Key].LeftHasKey = true;
            }
            foreach (var kv in right ?? [])
            {
                if (!merge.ContainsKey(kv.Key))
                {
                    merge.Add(kv.Key, new MergeJsonNode());
                }
                merge[kv.Key].Right = kv.Value;
            }

            bool first = true;
            foreach (var key in merge.Keys.Order())
            {
                var value = merge[key];
                if (!first)
                {
                    stringBuilder.AppendLine(",");
                }
                else
                {
                    first = false;
                    stringBuilder.AppendLine();
                }
                stringBuilder.Append($"{indent}  \"{key}\": ");
                var result = JsonValueTraverse(value.LeftHasKey, value.Left, value.Right, indent + "  ", arrayValueIndent: false);
                stringBuilder.Append(result.StringBuilder);
                computed |= result.Computed;
            }
            if (!first)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append($"{indent}");
            }
            stringBuilder.Append('}');
            return new JsonTraverseReturnValue { 
                StringBuilder = stringBuilder, 
                Computed = computed
            };
        }

        JsonTraverseReturnValue JsonArrayTraverse(JsonArray left, JsonArray? right, string indent)
        {
            var stringBuilder = new StringBuilder();
            bool computed = false;
            stringBuilder.Append('[');
            bool first = true;
            for (int i = 0; i < left.Count; i++)
            {
                if (!first)
                {
                    stringBuilder.AppendLine(",");
                }
                else
                {
                    first = false;
                    stringBuilder.AppendLine();
                }
                var result = JsonValueTraverse(true, left[i], (right?.Count >= i ? right[i] : null) ?? null, indent + "  ", arrayValueIndent: true);
                stringBuilder.Append(result.StringBuilder);
                computed |= result.Computed;
            }
            if (!first)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append($"{indent}");
            }
            stringBuilder.Append(']');
            return new JsonTraverseReturnValue { 
                StringBuilder = stringBuilder, 
                Computed = computed 
            };
        }
        
    }

}


