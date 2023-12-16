using Abraham.HomenetBase.Connectors;
using Abraham.HomenetBase.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;

namespace GoogleCalenderReaderCore
{
    class Program
    {
        private const string VERSION = "2023-12-13";

        #region ------------- Configuration -------------------------------------------------------
        private static string               _ServerURL;
        private static string               _Username;
        private static string               _Password;
        private static string               _GoogleCredentials;
		private static int                  _PushIntervalInMinutes;
		private static bool                 _LogToConsole;
		private static bool                 _LogToFile;
		private static string               _LogfileName;
        private static DataObjectsConnector _Connector;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
		{
			do
			{
				try
				{
					ReadConfiguration();
					Greeting();
					Endless_Loop();
				}
				catch (Exception ex)
				{
					Log("Exception in Main loop: " + ex.ToString());
					Log("Waiting 10 seconds...");
					Thread.Sleep(10000);
					Log("Trying again to read from google calendar...");
				}
			}
			while (true);
		}

        private static void Greeting()
		{
			Log($"---------------------------------------------------------------");
			Log($"Google Calendar Reader");
			Log($"Oliver Abraham, www.oliver-abraham.de, {VERSION}");
			Log($"Configuration read from file app.config");
			Log($"Update interval: {_PushIntervalInMinutes} minutes");
			Log($"---------------------------------------------------------------");
		}
        #endregion



        #region ------------- Implementation ------------------------------------------------------
		#region Main program
		private static void Endless_Loop()
        {
            Read_all_Dataobjects();

            DateTime NextPushMessage = DateTime.Now;
            for (; ; )
            {
                if (DateTime.Now > NextPushMessage)
                {
                    Action();

                    NextPushMessage = DateTime.Now.AddMinutes(_PushIntervalInMinutes);
                    Log($"Next action in {NextPushMessage}");
                }

                Thread.Sleep(10000);
            }
        }

        private static void Read_all_Dataobjects()
        {
            Log("Setting up home automation client");
            _Connector = new DataObjectsConnector(_ServerURL, _Username, _Password, 60);
        }

        private static void Action()
        {
            Log("Reading items from google calendar");
            GoogleCalendarReader Reader = new GoogleCalendarReader(_GoogleCredentials);
            var Events = Reader.Read_next_events_starting_at(DateTime.Today);//.AddDays(1));

            Find_event_and_update_hnserver(Events, "Abfuhr: Bio"        , "ABHOLUNG_BIOTONNE");
            Find_event_and_update_hnserver(Events, "Abfuhr: Restabfall" , "ABHOLUNG_RESTMUELL");
            Find_event_and_update_hnserver(Events, "Abfuhr: Papier"     , "ABHOLUNG_PAPIERTONNE");
            Find_event_and_update_hnserver(Events, "Abfuhr: Gelber Sack", "ABHOLUNG_GELBERSACK");
        }

        private static void Find_event_and_update_hnserver(List<GoogleCalendarReader.CalendarEvent> events, 
                                                           string string_to_search,
                                                           string data_object_name)
        {
            var NextEvent = Find_event_in_list(events, string_to_search);

            string FormattedValue;
            if (NextEvent != null)
            {
                FormattedValue = Format_date_and_time((DateTime)NextEvent);
            }
            else
            {
                FormattedValue = "----";
                Log($"     ---> Event could not be found in calendar !!!");
            }

            var Do = new DataObject();
            Do.Name = data_object_name;
            Do.Value= FormattedValue;

            Log($"     ---> setting data object {data_object_name} to {FormattedValue}");
            var success = _Connector.UpdateValueOnly(Do);
            if (!success)
                Log($"     ---> ERROR in communication! {_Connector.LastError}");
        }

        private static string Format_date_and_time(DateTime value)
        {
            if (value.Date == DateTime.Now.Date)
            {
                return "heute";
            }
            else if (value.Date == DateTime.Now.Date.AddDays(1))
            {
                return "morgen";
            }
            else if (value.Date == DateTime.Now.Date.AddDays(-1))
            {
                return "gestern";
            }
            else
            {
                return $"{value.Day}.{value.Month}.";
            }
        }

        private static DateTime? Find_event_in_list(List<GoogleCalendarReader.CalendarEvent> events, 
                                                    string string_to_search)
        {
            string_to_search = string_to_search.ToUpper();
            var Events = (from e in events
                          orderby e.When
                          where e.Summary.ToUpper().Contains(string_to_search)
                          select e);

            if (Events.Any())
            {
                var Next = Events.First();
                if (Next != null && Next.When != null)
                {
                    return (DateTime)Next.When;
                }
            }
            return null;
        }

        #endregion

        #region Configuration and Logging
        private static void ReadConfiguration()
        {
            Log("Reading configuration ...");
            _ServerURL               = ConfigurationManager.AppSettings["ServerURL"];
            _Username                = ConfigurationManager.AppSettings["Username"];
            _Password                = ConfigurationManager.AppSettings["Password"];
            _GoogleCredentials       = ConfigurationManager.AppSettings["GoogleCredentials"];
			_PushIntervalInMinutes   = Convert.ToInt32(ConfigurationManager.AppSettings["PushIntervalInMinutes"]);
			_LogToConsole            = Convert.ToBoolean(ConfigurationManager.AppSettings["LogToConsole"]);
			_LogToFile               = Convert.ToBoolean(ConfigurationManager.AppSettings["LogToFile"]);
			_LogfileName             = ConfigurationManager.AppSettings["LogfileName"];
            Log("OK");
        }

		private static void Log(string message)
		{
			if (_LogToConsole) 
			{
				Console.WriteLine(message);
			}
			
			if (_LogToFile) 
			{
				string Line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}    {message}\r\n";
				File.AppendAllText(_LogfileName, Line);
			}
		}
		#endregion

 		#endregion
   }
}
