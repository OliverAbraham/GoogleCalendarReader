using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;


namespace GoogleCalendarReader
{
    class GoogleCalendarReader
    {
        #region ------------- Types and constants -------------------------------------------------
        public class CalendarEvent
        {
            public string Summary { get; set; }
            public string Description { get; set; }
            public DateTime? When { get; internal set; }

            public override string ToString()
            {
                return $"{Summary} {When} {Description}";
            }
        }
        #endregion



        #region ------------- Properties ----------------------------------------------------------
        public string GoogleTokenDirectory { get; set; } = "token.json";
        public List<string> Messages { get; set; }
        #endregion



        #region ------------- Fields --------------------------------------------------------------
        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/calendar-dotnet-quickstart.json
        static string[] Scopes = { CalendarService.Scope.CalendarReadonly };
        static string ApplicationName = "Google Calendar API .NET Quickstart";
        private string _googleCredentialsFile;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public GoogleCalendarReader(string credentialsFile)
        {
            _googleCredentialsFile = credentialsFile;
        }
        #endregion



        #region ------------- Methods -------------------------------------------------------------
        public List<CalendarEvent> Read_next_events(int max_events_in_advance = 100)
        {
            return Read_next_events_starting_at(DateTime.Now, max_events_in_advance);
        }

        public List<CalendarEvent> Read_next_events_starting_at(DateTime point_of_time, int max_events_in_advance = 100)
        {
            Messages = new List<string>();

            UserCredential credential;
            using (var stream = new FileStream(_googleCredentialsFile, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(GoogleTokenDirectory, true)).Result;
                Messages.Add("Credential file saved to: " + GoogleTokenDirectory);
            }

            // Create Google Calendar API service.
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define parameters of request.
            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMin      = point_of_time;
            request.ShowDeleted  = false;
            request.SingleEvents = true;
            request.MaxResults   = max_events_in_advance;
            request.OrderBy      = EventsResource.ListRequest.OrderByEnum.StartTime;

            List<CalendarEvent> Results = new List<CalendarEvent>();

            // List events.
            Events events = request.Execute();
            Messages.Add("Upcoming events:");
            if (events.Items != null && events.Items.Count > 0)
            {
                foreach (var eventItem in events.Items)
                {
                    CalendarEvent New = new CalendarEvent();
                    New.Summary       = eventItem.Summary;
                    New.Description   = eventItem.Description;
                    if (eventItem.Start != null && eventItem.Start.DateTime != null)
                    {
                        New.When = eventItem.Start.DateTime;
                    }
                    else
                    {
                        if (DateTime.TryParse(eventItem.Start.Date, out DateTime Temp))
                            New.When = Temp;
                    }
                    Results.Add(New);

                    Messages.Add($"{New.Summary} ({New.When})");
                }
            }
            else
            {
                Messages.Add("No upcoming events found.");
            }

            return Results;
        }
        #endregion
    }
}
