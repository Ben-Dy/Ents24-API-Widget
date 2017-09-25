namespace Ents24_API_Widget
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Use the <c>Ents24</c> API to fetch a list of gigs happening in one area
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The <c>Ents24</c> ClientID. <see cref="https://developers.ents24.com/control-panel"/>
        /// </summary>
        private const string CLIENTID = "!!Register for your own!!";

        /// <summary>
        /// The <c>Ents24</c> Client Secret. (see link above)
        /// </summary>
        private const string CLIENTSECRET = "!!Register for your own!!";

        /// <summary>
        /// The URL where an access token can be obtained
        /// </summary>
        private const string AUTHURL = "https://api.ents24.com/auth/token";

        /// <summary>
        /// The API URL used to fetch lists of events. <see cref="https://developers.ents24.com/api-reference/event"/> 
        /// </summary>
        private const string LOCATIONURL = "https://api.ents24.com/event/list?location=geo:{0}&radius_distance={1}&distance_unit=mi&date_from={2}&date_to={3}&incl_image=0&order_by=lastUpdate&order_direction=desc&results_per_page=100";

        /// <summary>
        /// The latitude and longitude of the location to check for events
        /// </summary>
        private const string LATLONG = "50.842503343891,-0.13203927625894";

        /// <summary>
        /// The distance from the location in which to include events (in miles)
        /// </summary>
        private const string DISTANCE = "15";

        /// <summary>
        /// Kick off the work. Get an access token and then use that to fetch
        /// a list of events
        /// </summary>
        /// <param name="args">Start up arguments</param>
        public static void Main(string[] args)
        {
            string token = GetAccessToken();
            Gig[] eventsList = GetGigList(token);

            foreach (var gig in eventsList)
            {
                Console.WriteLine(gig.Venue.Name + " " + gig.Headline + " " + gig.StartDate + " " + gig.StartTimeString + " " + gig.Venue.Address.Town);
            }

            Console.Read();
        }

        /// <summary>
        /// Fetch the list of events from the API
        /// </summary>
        /// <param name="token">The access token required to authorize the API request</param>
        /// <returns>List of events</returns>
        private static Gig[] GetGigList(string token)
        {
            Gig[] gigResponse;

            WebRequest request = WebRequest.Create(string.Format(LOCATIONURL, LATLONG, DISTANCE, DateTime.Now.ToString("yyyy-MM-dd"), DateTime.Now.AddYears(1).ToString("yyyy-MM-dd")));
            request.Headers.Add("Authorization", token);
            request.Method = "GET";

            using (WebResponse response = request.GetResponse())
            {
                using (StreamReader streamy = new StreamReader(response.GetResponseStream()))
                {
                    gigResponse = Gig.FromJson(streamy.ReadToEnd());
                }
            }

            return gigResponse;
        }

        /// <summary>
        /// Fetch an access token from the API to be used later to authorize requests for data.
        /// First checks to see if an access token has already been recently fetched
        /// and if so just returns the token from local storage.
        /// </summary>
        /// <returns>access token</returns>
        private static string GetAccessToken()
        {
            string token = string.Empty;
            string postData = string.Format("client_id={0}&client_secret={1}", CLIENTID, CLIENTSECRET);

            if (!ExistingTokenStillValid(out token))
            {
                ASCIIEncoding encoding = new ASCIIEncoding();
                byte[] postDataBytes = encoding.GetBytes(postData);

                WebRequest request = WebRequest.Create(AUTHURL);
                request.ContentType = "application/x-www-form-urlencoded";
                request.Method = "POST";
                request.ContentLength = postDataBytes.Length;

                using (Stream newStream = request.GetRequestStream())
                {
                    newStream.Write(postDataBytes, 0, postDataBytes.Length);
                }

                using (WebResponse response = request.GetResponse())
                {
                    using (StreamReader streamy = new StreamReader(response.GetResponseStream()))
                    {
                        AuthenticationServerResponse authResponse = AuthenticationServerResponse.FromJson(streamy.ReadToEnd());
                        DateTime expiry = UnixTimeStampToDateTime(authResponse.Expires);
                        token = authResponse.AccessToken;
                        WriteTokenToFile(token, expiry);
                    }
                }
            }

            return token;
        }

        /// <summary>
        /// Store a token locally, ready to be read next time the API is accessed
        /// </summary>
        /// <param name="token">Access token</param>
        /// <param name="expiry">Access token expiry date</param>
        private static void WriteTokenToFile(string token, DateTime expiry)
        {
            string[] lines = { token, expiry.ToString() };
            File.WriteAllLines("token.txt", lines);
        }

        /// <summary>
        /// Is there an access token stored locally that is still valid for use.
        /// Access tokens normally last 60 days.
        /// </summary>
        /// <param name="token">Access token</param>
        /// <returns>True if the access token is still valid</returns>
        private static bool ExistingTokenStillValid(out string token)
        {
            bool result = false;
            token = string.Empty;

            try
            {
                string[] lines = File.ReadAllLines("token.txt");

                if (lines.Length == 2)
                {
                    DateTime expiry;
                    if (DateTime.TryParse(lines[1], out expiry))
                    {
                        if (DateTime.Now < expiry.AddDays(-1))
                        {
                            token = lines[0];
                            result = true;
                        }
                    }
                    else
                    {
                        File.Delete("token.txt");
                        throw new Exception("Expiry date in wrong format");
                    }
                }
                else
                {
                    File.Delete("token.txt");
                    throw new Exception("File contents not in expected format");
                }
            }
            catch (Exception)
            {
                // do nothing
            }

            return result;
        }

        /// <summary>
        /// Convert a UNIX time stamp to a date and time.
        /// </summary>
        /// <param name="unixTimeStamp">UNIX format date and time</param>
        /// <returns>Date and time</returns>
        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            System.DateTime convertedDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            convertedDateTime = convertedDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return convertedDateTime;
        }
    }

    /// <summary>
    /// Class used to convert the response from the API when an access
    /// token is requested. 
    /// </summary>
    public class AuthenticationServerResponse
    {
        [JsonProperty("expires")]
        public long Expires { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        public static AuthenticationServerResponse FromJson(string json)
        {
            return JsonConvert.DeserializeObject<AuthenticationServerResponse>(json, Converter.Settings);
        }
    }

    /// <summary>
    /// Class used to convert and store an event when returned from the API
    /// </summary>
    public class Gig
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("fansOnEnts24")]
        public long FansOnEnts24 { get; set; }

        [JsonProperty("endDate")]
        public string EndDate { get; set; }

        [JsonProperty("description")]
        public object Description { get; set; }

        [JsonProperty("endTimeString")]
        public object EndTimeString { get; set; }

        [JsonProperty("hasMoved")]
        public bool HasMoved { get; set; }

        [JsonProperty("genre")]
        public string[] Genre { get; set; }

        [JsonProperty("headline")]
        public string Headline { get; set; }

        [JsonProperty("isRescheduled")]
        public bool IsRescheduled { get; set; }

        [JsonProperty("startDate")]
        public string StartDate { get; set; }

        [JsonProperty("isFree")]
        public bool IsFree { get; set; }

        [JsonProperty("isCancelled")]
        public bool IsCancelled { get; set; }

        [JsonProperty("isPostponed")]
        public bool IsPostponed { get; set; }

        [JsonProperty("lastUpdate")]
        public string LastUpdate { get; set; }

        [JsonProperty("isSoldOut")]
        public bool IsSoldOut { get; set; }

        [JsonProperty("price")]
        public object Price { get; set; }

        [JsonProperty("ticketsAvailable")]
        public bool TicketsAvailable { get; set; }

        [JsonProperty("venue")]
        public Venue Venue { get; set; }

        [JsonProperty("startTimeString")]
        public string StartTimeString { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("webLink")]
        public string WebLink { get; set; }

        public static Gig[] FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Gig[]>(json, Converter.Settings);
        }
    }

    /// <summary>
    /// Definition of a Venue object
    /// </summary>
    public partial class Venue
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("location")]
        public Location Location { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("webLink")]
        public string WebLink { get; set; }
    }

    /// <summary>
    /// Definition of an Address object
    /// </summary>
    public partial class Address
    {
        [JsonProperty("county")]
        public string County { get; set; }

        [JsonProperty("streetAddress")]
        public string[] StreetAddress { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("postcode")]
        public string Postcode { get; set; }

        [JsonProperty("town")]
        public string Town { get; set; }
    }

    /// <summary>
    /// Definition of a Location object
    /// </summary>
    public partial class Location
    {
        [JsonProperty("lat")]
        public double Lat { get; set; }

        [JsonProperty("lon")]
        public double Lon { get; set; }
    }

    /// <summary>
    /// JSon conversion settings
    /// </summary>
    public class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
        };
    }
}
