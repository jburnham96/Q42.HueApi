﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Q42.HueApi.Extensions;
using Newtonsoft.Json;
using Q42.HueApi.Models.Groups;
using Q42.HueApi.Models;
using System.Dynamic;

namespace Q42.HueApi
{
  /// <summary>
  /// Partial HueClient, contains requests to the /scenes/ url
  /// </summary>
  public partial class HueClient
  {


    /// <summary>
    /// Asynchronously gets all scenes registered with the bridge.
    /// </summary>
    /// <returns>An enumerable of <see cref="Scene"/>s registered with the bridge.</returns>
    public async Task<IReadOnlyCollection<Scene>> GetScenesAsync()
    {
      CheckInitialized();

      HttpClient client = HueClient.GetHttpClient();
      string stringResult = await client.GetStringAsync(new Uri(String.Format("{0}scenes", ApiBase))).ConfigureAwait(false);

#if DEBUG
      stringResult = "{    \"1\": {        \"name\": \"My Scene 1\",        \"lights\": [            \"1\",            \"2\",            \"3\"        ],        \"recycle\": true    },    \"2\": {        \"name\": \"My Scene 2\",        \"lights\": [            \"1\",            \"2\",            \"3\"        ],        \"recycle\": true    }}";
#endif


      List<Scene> results = new List<Scene>();

      JToken token = JToken.Parse(stringResult);
      if (token.Type == JTokenType.Object)
      {
        //Each property is a scene
        var jsonResult = (JObject)token;

        foreach (var prop in jsonResult.Properties())
        {
          Scene scene = JsonConvert.DeserializeObject<Scene>(prop.Value.ToString());
          scene.Id = prop.Name;
          
          results.Add(scene);
        }

      }

      return results;

    }

		public async Task<string> CreateSceneAsync(Scene scene)
		{
			CheckInitialized();

			if (scene == null)
				//throw new ArgumentNullException(nameof(scene)); TODO: Repair all of these
			if (scene.Lights == null)
				//throw new ArgumentNullException(nameof(scene.Lights));
			if (scene.Name == null)
				//throw new ArgumentNullException(nameof(scene.Name));

			//Filter non updatable properties
			scene.FilterNonUpdatableProperties();

            //It defaults to false, but fails when omitted
            //https://github.com/Q42/Q42.HueApi/issues/56
            if (!scene.Recycle.HasValue)
                scene.Recycle = false;

			string jsonString = JsonConvert.SerializeObject(scene, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

			HttpClient client = HueClient.GetHttpClient();
			var response = await client.PostAsync(new Uri(String.Format("{0}scenes", ApiBase)), new StringContent(jsonString)).ConfigureAwait(false);

			var jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

			HueResults sceneResult = DeserializeDefaultHueResult(jsonResult);

			if (sceneResult.Count > 0 && sceneResult[0].Success != null && !string.IsNullOrEmpty(sceneResult[0].Success.Id))
			{
				return sceneResult[0].Success.Id;
			}

			if (sceneResult.HasErrors())
				throw new Exception(sceneResult.Errors.First().Error.Description);

			return null;

		}

		/// <summary>
		/// UpdateSceneAsync
		/// </summary>
		/// <param name="id"></param>
		/// <param name="name"></param>
		/// <param name="lights"></param>
		/// <param name="storeLightState">If set, the lightstates of the lights in the scene will be overwritten by the current state of the lights. Can also be used in combination with transitiontime to update the transition time of a scene.</param>
		/// <param name="transitionTime">Can be used in combination with storeLightState</param>
		/// <returns></returns>
		public async Task<HueResults> UpdateSceneAsync(string id, string name, IEnumerable<string> lights, bool? storeLightState = null, TimeSpan? transitionTime = null)
	{
		CheckInitialized();

		if (id == null)
			throw new ArgumentNullException("id");
		if (id.Trim() == String.Empty)
			throw new ArgumentException("id must not be empty", "id");
		if (lights == null)
			throw new ArgumentNullException("lights");

		dynamic jsonObj = new ExpandoObject();
		jsonObj.lights = lights;

		if (storeLightState.HasValue)
		{
			jsonObj.storelightstate = storeLightState.Value;

			//Transitiontime can only be used in combination with storeLightState
			if (transitionTime.HasValue)
			{
				jsonObj.transitiontime = (uint)transitionTime.Value.TotalSeconds * 10;
			}
		}

		if (!string.IsNullOrEmpty(name))
			jsonObj.name = name;

		string jsonString = JsonConvert.SerializeObject(jsonObj, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

		HttpClient client = HueClient.GetHttpClient();
		var response = await client.PutAsync(new Uri(String.Format("{0}scenes/{1}", ApiBase, id)), new StringContent(jsonString)).ConfigureAwait(false);

		var jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		return DeserializeDefaultHueResult(jsonResult);

	}

		/// <summary>
		/// UpdateSceneAsync
		/// </summary>
		/// <param name="id"></param>
		/// <param name="scene"></param>
		/// <returns></returns>
		public async Task<HueResults> UpdateSceneAsync(string id, Scene scene)
		{
			CheckInitialized();

			if (id == null)
				throw new ArgumentNullException("id");
			if (id.Trim() == String.Empty)
				throw new ArgumentException("id must not be empty", "id");
			if (scene == null)
				//throw new ArgumentNullException(nameof(scene));

			//Set these fields to null
			scene.Id = null;
			scene.Recycle = null;
			scene.FilterNonUpdatableProperties();

			string jsonString = JsonConvert.SerializeObject(scene, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

			HttpClient client = HueClient.GetHttpClient();
			var response = await client.PutAsync(new Uri(String.Format("{0}scenes/{1}", ApiBase, id)), new StringContent(jsonString)).ConfigureAwait(false);

			var jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

			return DeserializeDefaultHueResult(jsonResult);

		}


		public async Task<HueResults> ModifySceneAsync(string sceneId, string lightId, LightCommand command)
    {
      CheckInitialized();

      if (sceneId == null)
        throw new ArgumentNullException("sceneId");
      if (sceneId.Trim() == String.Empty)
        throw new ArgumentException("sceneId must not be empty", "sceneId");
      if (lightId == null)
        throw new ArgumentNullException("lightId");
      if (lightId.Trim() == String.Empty)
        throw new ArgumentException("lightId must not be empty", "lightId");

      if (command == null)
        throw new ArgumentNullException("command");

      string jsonCommand = JsonConvert.SerializeObject(command, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

      HttpClient client = HueClient.GetHttpClient();
      var response = await client.PutAsync(new Uri(String.Format("{0}scenes/{1}/lights/{1}/lightstate", ApiBase, sceneId, lightId)), new StringContent(jsonCommand)).ConfigureAwait(false);

      var jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

      return DeserializeDefaultHueResult(jsonResult);
    }


    public Task<HueResults> RecallSceneAsync(string sceneId, string groupId = "0")
    {
      if (sceneId == null)
        throw new ArgumentNullException("sceneId");

      var groupCommand = new SceneCommand() { Scene = sceneId };

      return this.SendGroupCommandAsync(groupCommand, groupId);

    }

	/// <summary>
	/// Deletes a scene
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public async Task<HueResults> DeleteSceneAsync(string sceneId)
	{
		CheckInitialized();

		HttpClient client = HueClient.GetHttpClient();
		var result = await client.DeleteAsync(new Uri(String.Format("{0}scenes/{1}", ApiBase, sceneId))).ConfigureAwait(false);

		string jsonResult = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

		return DeserializeDefaultHueResult(jsonResult);

	}

		/// <summary>
		/// Get a single scene
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public async Task<Scene> GetSceneAsync(string id)
		{
			CheckInitialized();

			HttpClient client = HueClient.GetHttpClient();
			string stringResult = await client.GetStringAsync(new Uri(String.Format("{0}scenes/{1}", ApiBase, id))).ConfigureAwait(false);

			Scene scene = DeserializeResult<Scene>(stringResult);

			if (string.IsNullOrEmpty(scene.Id))
				scene.Id = id;

			return scene;

		}
	}
}
