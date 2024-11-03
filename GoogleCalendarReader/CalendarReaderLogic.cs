using Abraham.ProgramSettingsManager;
using Abraham.HomenetBase.Connectors;
using Abraham.HomenetBase.Models;
using Abraham.MQTTClient;

namespace GoogleCalendarReader
{
    class CalendarReaderLogic
    {
        #region ------------- Properties ---------------------------------------------------------
        public int UpdateIntervalInMinutes { get { return (_config is not null) ? _config.UpdateIntervalInMinutes : 1;} }
        public int MaxLogMessagesInUI { get { return (_config is not null) ? ((_config.MaxLogMessagesInUI < 1) ? 1 : _config.MaxLogMessagesInUI) : 1000; } }

        public delegate void LoggerDelegate(string message);
        public LoggerDelegate Logger { get; set; }
        #endregion



        #region ------------- Fields --------------------------------------------------------------
        #region Configuration
        private class Configuration
        {
            public string      ServerURL               { get; set; }
            public string      Username                { get; set; }
            public string      Password                { get; set; }
            public string      MqttServerURL           { get; set; }
            public string      MqttUsername            { get; set; }
            public string      MqttPassword            { get; set; }
            public int         ServerTimeout           { get; set; }
            public int         MaxCalendarEventsToRead { get; set; }
            public int         MaxLogMessagesInUI      { get; set; }
            public int         UpdateIntervalInMinutes { get; set; }
            public string      TxtTomorrow             { get; set; }
            public string      TxtToday                { get; set; }
            public string      TxtYesterday            { get; set; }
            public List<Event> Events                  { get; set; }

            public override string ToString()
            {
                return 
                    $"Home Automation Server  : {ServerURL} / {Username} / ***************\n" +
                    $"MQTT Broker             : {MqttServerURL} / {MqttUsername} / ***************\n" +
                    $"ServerTimeout           : {ServerTimeout}\n" +
                    $"MaxLogMessagesInUI      : {MaxLogMessagesInUI}\n" +
                    $"UpdateIntervalInMinutes : {UpdateIntervalInMinutes}\n" +
                    $"TxtTomorrow             : {TxtTomorrow}\n" +
                    $"TxtToday                : {TxtToday}\n" +
                    $"TxtYesterday            : {TxtYesterday}\n" +
                    $"Events                  :\n" + string.Join("\n", Events);
            }
        }

        private class Event
        {
            public string CalendarEventTitle { get; set; }
            public string DataObjectName { get; set; }
            public string MqttTopic { get; set; }

            public override string ToString()
            {
                return $"    CalendarEventTitle : {CalendarEventTitle}\n" +
                       $"    DataObject         : {DataObjectName}\n" +
                       $"    MQTT topic         : {MqttTopic}\n" +
                       $"    \n";
            }
        }

        private Configuration _config;
        private ProgramSettingsManager<Configuration> _configurationManager;
        
        // These file locations will be tried. The first file found will be used as configuration file
        private string[] _settingsFileOptions = new string[]
        {
            @"C:\Credentials\GoogleCalendarReader\appsettings.hjson", // used for local testing
            "/opt/appsettings.hjson",                                 // used for running in docker container
            "./appsettings.hjson",                                    // used for running without a container
        };
        
        // contains the filename of the configuration file that was actually chosen
        private string _configurationFilename = "";
        // contains the filename of the google credentials file (json) that is in the same folder as the configuration file
        private string _googleCredentialsFile;
        #endregion
        #region Server connections
        private static DataObjectsConnector _homenetClient;
        private static MQTTClient _mqttClient;
        private string _googleTokenSubdir;
        #endregion
        #endregion



        #region ------------- Ctor ----------------------------------------------------------------
        public CalendarReaderLogic()
        {
            Logger = (message) => {};
        }
        #endregion



        #region ------------- Methods -------------------------------------------------------------
        /// <summary>
        /// returns true if configuration was read successfully.
        /// </summary>
        public bool ReadConfiguration()
        {
            foreach (var option in _settingsFileOptions)
            {
                var success = TryOption(option);
                if (success)
                    return true;
            }

            return ReadConfiguration_error();
        }

        private bool TryOption(string option)
        {
            Logger($"Trying to read configuration from '{option}'...");
            if (!File.Exists(option))
                return false;

            var success = ReadConfiguration_internal(option);
            if (!success)
                return false;

            Logger($"Found file '{option}', assuming file with google credentials (credentials.json) is in same folder");
            var path = Path.GetFullPath(option);
            path = path.Replace(Path.GetFileName(path), "");
            _googleCredentialsFile = Path.Combine(path, "credentials.json");
            if (!File.Exists(_googleCredentialsFile))
            {
                Logger($"Cannot find file '{_googleCredentialsFile}', trying next folder");
                return false;
            }

            Logger($"Found file '{_googleCredentialsFile}'. Assuming the Google token subdirectory token.json is also here or can be created.");
            _googleTokenSubdir = Path.Combine(path, "token.json");
            
            if (Directory.Exists(_googleTokenSubdir))
            {
                Logger($"Found token subdirectory '{_googleTokenSubdir}'");
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(_googleTokenSubdir, "token.json"));
                    Logger($"Cannot find token subdirectory '{_googleTokenSubdir}', but it could be created");
                }
                catch (Exception ex)
                {
                    Logger($"token subdirectory '{_googleTokenSubdir}' cannot be created!");
                }
            }

            CheckIfTheTokenSubdirectoryIsWritable();
            return true;
        }

        private void CheckIfTheTokenSubdirectoryIsWritable()
        {
            // If the token directory exists, but is write-protected (because we got it from a Kubernetes Configmap),
            // we create a writable temp directory and make a copy of it
            if (!Directory.Exists(_googleTokenSubdir))
                return;

            var testFile = Path.Combine(_googleTokenSubdir, "test.txt");
            try
            {
                File.Create(testFile);
                Logger($"Token directory '{_googleTokenSubdir}' is writable. All ok.");

                try
                {
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                }
                return;
            }
            catch (Exception ex)
            {
                Logger($"Cannot write to token directory '{_googleTokenSubdir}', trying to create a writable temp directory");
            }


            var subdir = "token.json.tmp";
            try
            {
                Directory.CreateDirectory(subdir);
            }
            catch (Exception ex)
            {
                Logger($"Cannot create temp subdirectory '{subdir}'");
                return;
            }

            try
            {
                foreach (var file in Directory.GetFiles(_googleTokenSubdir))
                {
                    var targetFile = Path.Combine(subdir, Path.GetFileName(file));
                    File.Copy(file, targetFile);
                }
                Logger($"Created temp directory '{subdir}' and copied the contents of '{_googleTokenSubdir}' into it.");
                _googleTokenSubdir = subdir;
            }
            catch (Exception ex)
            {
                Logger($"Cannot copy files to temp directory '{subdir}'");
                Logger($"More Info: {ex}");
            }
        }

        /// <summary>
        /// prints the configuration to the log
        /// </summary>
        public void LogConfiguration()
        {
            Logger("");
            Logger("Configuration:");
            var lines = _config.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            lines.ForEach(line => Logger(line));
            Logger("");
        }

        /// <summary>
        /// Runs the check and sends the results to the home automation server.
        /// </summary>
        public void ReadCalendar()
        {
            var connected = ConnectToServer();
            if (!connected)
            {
                Logger("Error connecting to Homenet/MQTT server");
                return;
            }

            Logger("Reading items from google calendar");
            try
            {
                var reader = new GoogleCalendarReader(_googleCredentialsFile);
                reader.GoogleTokenDirectory = _googleTokenSubdir;
                var allCalendarEvents = reader.Read_next_events_starting_at(DateTime.Today, max_events_in_advance: _config.MaxCalendarEventsToRead);
                Logger($"{allCalendarEvents.Count} calender entries were read");

                foreach(var @event in _config.Events)
                {
                    FindEventAndUpdateServer(allCalendarEvents, @event.CalendarEventTitle, @event.DataObjectName, @event.MqttTopic);
                }
            }
            catch (Exception ex)
            {
                Logger($"Error reading calendar: {ex}");
            }
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        #region Reading
        private void FindEventAndUpdateServer(List<GoogleCalendarReader.CalendarEvent> allCalendarEvents, 
                                              string title,
                                              string data_object_name,
                                              string mqttTopic)
        {
            var NextEvent = FindEventByTitle(allCalendarEvents, title);

            string formattedValue;
            if (NextEvent != null)
            {
                formattedValue = Format_date_and_time((DateTime)NextEvent);
            }
            else
            {
                formattedValue = "----";
                Logger($"     ---> Event could not be found in calendar !!!");
            }

            Logger($"     ---> found {title,-30}, setting {data_object_name,-30} / {mqttTopic,-50} to '{formattedValue}'");
            UpdateValueInServer(formattedValue, data_object_name, mqttTopic);
        }

        private string Format_date_and_time(DateTime value)
        {
            if (value.Date == DateTime.Now.Date)
            {
                return _config.TxtToday;
            }
            else if (value.Date == DateTime.Now.Date.AddDays(1))
            {
                return _config.TxtTomorrow;
            }
            else if (value.Date == DateTime.Now.Date.AddDays(-1))
            {
                return _config.TxtYesterday;
            }
            else
            {
                return $"{value.Day}.{value.Month}.";
            }
        }

        private DateTime? FindEventByTitle(List<GoogleCalendarReader.CalendarEvent> allEvents, string title)
        {
            title = title.ToUpper();
            var eventsWithThisTitle = (from e in allEvents
                                      orderby e.When
                                      where e.Summary.ToUpper().Contains(title)
                                      select e);

            if (eventsWithThisTitle.Any())
            {
                var next = eventsWithThisTitle.First();
                if (next != null && next.When != null)
                {
                    return (DateTime)next.When;
                }
            }
            return null;
        }
        #endregion
        #region Sending results
        private bool ConnectToServer()
        {
            if (HomenetServerIsConfigured())
            {
                if (ConnectToHomenetServer())
                    return true;
            }

            if (MqttBrokerIsConfigured())
            {
                if (ConnectToMqttBroker())
                    return true;
            }

            return false;
        }

        private void UpdateValueInServer(string value, string dataObjectName, string mqttTopic)
        {
            if (HomenetServerIsConfigured())
                UpdateDataObject(value, dataObjectName);

            if (MqttBrokerIsConfigured())
                UpdateTopic(value, mqttTopic);
        }
        #endregion
        #region Home automation server connection
        private bool HomenetServerIsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.ServerURL) && 
                   !string.IsNullOrWhiteSpace(_config.Username) && 
                   !string.IsNullOrWhiteSpace(_config.Password);
        }

        private bool ConnectToHomenetServer()
        {
            Logger("Connecting to homenet server...");
            try
            {
                _homenetClient = new DataObjectsConnector(_config.ServerURL, _config.Username, _config.Password, _config.ServerTimeout);
                Logger("Connect successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger("Error connecting to homenet server:\n" + ex.ToString());
                return false;
            }
        }

        private void UpdateDataObject(string value, string dataObjectName)
        {
            if (_homenetClient is null)
                return;

            try
            {
                bool success = _homenetClient.UpdateValueOnly(new DataObject() { Name = dataObjectName, Value = value});
                if (!success)
                    Logger($"server update error! {_homenetClient.LastError}");
            }
            catch (Exception ex)
            {
                Logger("Error communicating with homenet server:\n" + ex.ToString());
            }
        }
        #endregion
        #region MQTT broker connection
        private bool MqttBrokerIsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_config.MqttServerURL) && 
                   !string.IsNullOrWhiteSpace(_config.MqttUsername) && 
                   !string.IsNullOrWhiteSpace(_config.MqttPassword);
        }

        private bool ConnectToMqttBroker()
        {
            Logger("Connecting to MQTT broker...");
            try
            {
                _mqttClient = new MQTTClient()
                    .UseUrl(_config.MqttServerURL)
                    .UseUsername(_config.MqttUsername)
                    .UsePassword(_config.MqttPassword)
                    .UseTimeout(_config.ServerTimeout)
                    .UseLogger(delegate(string message) { Logger(message); })
                    .Build();

                Logger("Created MQTT client");
                return true;
            }
            catch (Exception ex)
            {
                Logger("Error connecting to homenet server:\n" + ex.ToString());
                return false;
            }
        }

        private void UpdateTopic(string value, string topicName)
        {
            if (_mqttClient is null || topicName is null)
                return;

            try
            {
                var result = _mqttClient.Publish(topicName, value);

                if (!result.IsSuccess)
                    Logger($"MQTT topic update error! {result.ReasonString}");
            }
            catch (Exception ex)
            {
                Logger("Error communicating with MQTT server:\n" + ex.ToString());
            }
        }
        #endregion
        #region Reading configuration file
        private bool ReadConfiguration_internal(string filename)
        {
            try
            {
                Logger($"success");
                _configurationFilename = filename;

                _configurationManager = new ProgramSettingsManager<Configuration>()
                    .UseFullPathAndFilename(_configurationFilename)
                    .Load();

                _config = _configurationManager.Data;
                if (_config == null)
                {
                    Logger($"No valid configuration found!\nExpecting file '{_configurationFilename}'");
                    return false;
                }

                if (_config.MaxLogMessagesInUI < 1)
                    Logger($"Warning: Parameter '{nameof(_config.MaxLogMessagesInUI)}' is NOT set!");

                Logger($"Configuration read from filename '{_configurationFilename}'");
                return true;
            }
            catch (Exception ex)
            {
                Logger($"There was a problem reading the configuration file '{_configurationFilename}'");
                Logger($"Please check the contents");
                Logger($"More Info: {ex}");
                return false;
            }
        }

        private bool ReadConfiguration_error()
        {
            Logger($"No configuration file found!");
            return false;
        }

        private void WriteConfiguration_internal()
        {
            _configurationManager.Save(_config);
        }
        #endregion
        #endregion
    }
}