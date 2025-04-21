using Microsoft.AspNetCore.Mvc;
using PKHeX.Core;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace serenity.Controllers {
    [ApiController]
    [Route("api")]
    public class ServiceController : ControllerBase {
        [HttpPost("islegal")]
        public IActionResult CheckLegality([FromBody] PokemonRequest request) {
            try {
                byte[] data = Convert.FromBase64String(request.pkmdata);

                PKM pkm = (PKM)FileUtil.GetSupportedFile(data, ".pk");
                if (pkm == null)
                    return BadRequest(new { error = "Invalid Pokémon data format" });

                var la = new LegalityAnalysis(pkm);

                var response = new {
                    isLegal = la.Valid,
                    reasons = la.Results.Select(r => new {
                        identifier = r.Identifier.ToString(),
                        comment = r.Comment,
                        valid = r.Valid
                    }).ToArray()
                };

                return Ok(response);
            } catch (Exception ex) {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("pkmdata")]
        public IActionResult GetPokemonData([FromBody] PokemonRequest request) {
            try {
                byte[] data = Convert.FromBase64String(request.pkmdata);
                if (data == null || data.Length == 0) {
                    return BadRequest(new { error = "Invalid pokemon data" });
                }

                PKM pkm = (PKM)FileUtil.GetSupportedFile(data, ".pk");
                if (pkm == null)
                    return BadRequest(new { error = "Invalid Pokémon data format" });

                var speciesName = SpeciesName.GetSpeciesNameGeneration(pkm.Species, pkm.Language, pkm.Format);
                var speciesVariations = new List<string>();

                for (int lang = 0; lang <= 10; lang++) {
                    string langSpeciesName = SpeciesName.GetSpeciesNameGeneration(pkm.Species, lang, pkm.Format);
                    speciesVariations.Add(string.IsNullOrEmpty(langSpeciesName) ? "" : langSpeciesName);
                }

                var settings = new JsonSerializerSettings {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.None,
                    MaxDepth = 64,
                    TypeNameHandling = TypeNameHandling.None,
                    Error = (sender, args) => {
                        args.ErrorContext.Handled = true;
                    }
                };

                var json = JsonConvert.SerializeObject(pkm, settings);
                var pkmObject = Newtonsoft.Json.Linq.JObject.Parse(json);
                pkmObject["SpeciesNativeName"] = speciesName;
                pkmObject["SpeciesName"] = JArray.FromObject(speciesVariations);

                json = pkmObject.ToString(Formatting.None);
                return Content(json, "application/json");
            } catch (Exception ex) {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("savedata")]
        public IActionResult GetSaveData([FromBody] SaveFileRequest request) {
            try {
                byte[] data = Convert.FromBase64String(request.savedata);
                if (data == null || data.Length == 0) {
                    return BadRequest(new { error = "Invalid save data" });
                }

                SaveFile? sav = SaveUtil.GetVariantSAV(data);
                if (sav == null || !typeof(SaveFile).IsAssignableFrom(sav.GetType())) {
                    return BadRequest(new { error = "Invalid save file format" });
                }

                var settings = new Newtonsoft.Json.JsonSerializerSettings {
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                    Formatting = Newtonsoft.Json.Formatting.None,
                    MaxDepth = 64,
                    TypeNameHandling = TypeNameHandling.None,
                    Error = (sender, args) => {
                        args.ErrorContext.Handled = true;
                    }
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(sav, settings);
                var jsonobj = Newtonsoft.Json.Linq.JObject.Parse(json);

                void RemoveDataFields(Newtonsoft.Json.Linq.JToken token) {
                    if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object) {
                        var obj = (Newtonsoft.Json.Linq.JObject)token;
                        obj.Property("EncryptedPartyData")?.Remove();
                        obj.Property("EncryptedBoxData")?.Remove();
                        obj.Property("DecryptedPartyData")?.Remove();
                        obj.Property("DecryptedBoxData")?.Remove();
                        obj.Property("Zukan")?.Remove();
                        foreach (var property in obj.Properties()) {
                            RemoveDataFields(property.Value);
                        }
                    } else if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                        foreach (var item in (Newtonsoft.Json.Linq.JArray)token) {
                            RemoveDataFields(item);
                        }
                        if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                            foreach (var item in (Newtonsoft.Json.Linq.JArray)token) {
                                RemoveDataFields(item);
                            }
                        }
                    }
                }

                void RemoveInvalidPKMs(Newtonsoft.Json.Linq.JToken token) {
                    if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object) {
                        var obj = (Newtonsoft.Json.Linq.JObject)token;
                        foreach (var property in obj.Properties()) {
                            if (property.Name == "PartyData" || property.Name == "BoxData") {
                                if (property.Value.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                                    var array = (Newtonsoft.Json.Linq.JArray)property.Value;
                                    for (int i = array.Count - 1; i >= 0; i--) {
                                        var pkm = array[i];
                                        if (pkm == null || (pkm["Species"] != null && pkm.Value<int>("Species") == 0) || (pkm["SpeciesID"] != null && pkm.Value<int>("SpeciesID") == 0)) {
                                            array.RemoveAt(i);
                                        }
                                    }
                                }
                            }
                            RemoveInvalidPKMs(property.Value);
                        }
                    } else if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                        foreach (var item in (Newtonsoft.Json.Linq.JArray)token) {
                            RemoveInvalidPKMs(item);
                        }
                    }
                }

                RemoveDataFields(jsonobj);
                RemoveInvalidPKMs(jsonobj);

                foreach (var property in jsonobj.Properties()) {
                    if (property.Name == "PartyData" || property.Name == "BoxData") {
                        if (property.Value.Type == Newtonsoft.Json.Linq.JTokenType.Array) {
                            var array = (Newtonsoft.Json.Linq.JArray)property.Value;
                            foreach (var pkm in array) {
                                if (pkm.Value<bool>("ChecksumValid")) {
                                    var speciesName = SpeciesName.GetSpeciesNameGeneration(pkm.Value<ushort>("Species"), pkm.Value<int>("Language"), pkm.Value<byte>("Format"));
                                    var speciesVariations = new List<string>();
                                    for (int lang = 0; lang <= 10; lang++) {
                                        string langSpeciesName = SpeciesName.GetSpeciesNameGeneration(pkm.Value<ushort>("Species"), lang, pkm.Value<byte>("Format"));
                                        speciesVariations.Add(string.IsNullOrEmpty(langSpeciesName) ? "" : langSpeciesName);
                                    }
                                    pkm["SpeciesNativeName"] = speciesName;
                                    pkm["SpeciesName"] = JArray.FromObject(speciesVariations);
                                } else {
                                    string badEggText = pkm.Value<int>("Generation") == 3 ? "Bad EGG" : "Bad Egg";
                                    pkm["SpeciesNativeName"] = badEggText;

                                    var badEggVariations = new List<string>();
                                    for (int lang = 0; lang <= 10; lang++) {
                                        badEggVariations.Add(badEggText);
                                    }
                                    pkm["SpeciesName"] = JArray.FromObject(badEggVariations);
                                }
                            }
                        }
                    }
                }

                json = jsonobj.ToString();
                return Content(json, "application/json");
            } catch (Exception ex) {
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    public class PokemonRequest {
        public string pkmdata { get; set; } = string.Empty;
    }

    public class SaveFileRequest {
        public string savedata { get; set; } = string.Empty;
    }

    public class CheckResultDetail {
        public string Identifier { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public bool Valid { get; set; }
    }
}