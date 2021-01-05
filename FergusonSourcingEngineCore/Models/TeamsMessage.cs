using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

namespace FergusonSourcingCore.Models
{
    public class TeamsMessage
    {
        public TeamsMessage(string title, string text, string color, string teamsUrl)
        {
            Title = title;
            Text = text;
            ThemeColor = GetColorHex(color);
            TeamsUrl = teamsUrl;
        }

        private string GetColorHex(string color)
        {
            string colorHex;

            if (color == "red")
            {
                colorHex = "CD2626";
            }
            else if (color == "green")
            {
                colorHex = "3D8B37";
            }
            else if (color == "yellow")
            {
                colorHex = "FFFF33";
            }
            else if (color == "purple")
            {
                colorHex = "7F00FF";
            }
            else
            {
                colorHex = "F8F8FF"; // White
            }

            return colorHex;
        }

        public void LogToTeams(TeamsMessage teamsMessage)
        {
            try
            {
                using (var client = new LongTimeOutWebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";

                    var jsonRequest = JsonConvert.SerializeObject(teamsMessage);

                    client.UploadString(teamsMessage.TeamsUrl, jsonRequest);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("@context")]
        public string Context { get; set; }

        public string Title { get; set; }

        [JsonProperty("sections")]
        public List<Section> Sections { get; set; }

        [JsonProperty("potentialAction")]
        public List<PotentialAction> PotentialActions { get; set; }

        public string Text { get; set; }

        public string ThemeColor { get; set; }

        public string TeamsUrl { get; set; }
    }


    public class Section
    {
        public Section(string activityTitle, string activityImage, List<Fact> facts)
        {
            ActivityTitle = activityTitle;
            ActivityImage = activityImage;
            Facts = facts;
        }

        [JsonProperty("activityTitle")]
        public string ActivityTitle { get; set; }

        [JsonProperty("activitySubtitle")]
        public string ActivitySubtitle { get; set; }

        [JsonProperty("activityImage")]
        public string ActivityImage { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("facts")]
        public List<Fact> Facts { get; set; }
    }


    public class PotentialAction
    {
        [JsonProperty("@type")]
        public string Type { get; set; }

        public string Name { get; set; }

        public List<Target> Targets { get; set; }
    }


    public class Target
    {
        public string OS { get; set; }

        public string URI { get; set; }
    }


    public class Fact
    {
        public Fact(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; set; }

        public string Value { get; set; }
    }


    public class LongTimeOutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = 900000;
            return w;
        }
    }
}
