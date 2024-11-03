using Abraham.Scheduler;
using GoogleCalendarReader;
using NLog;

namespace WeatherGateway
{
    public class Program
    {
        #region ------------- Fields --------------------------------------------------------------
        private static CalendarReaderLogic _logic;
        private static Scheduler _scheduler;
        private static List<string> _log = new();
        private static Logger _logger;
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        public static void Main(string[] args)
        {
            InitLogging();
            Greeting();
            if (!InitWorker())
            {
                Console.WriteLine("waiting 120 seconds before exiting the process...");
                Thread.Sleep(120 * 1000);
                return;
            }
            Divider();
            InitScheduler();
            InitAndStartWebServer(args);
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        private static void Greeting()
        {
            Log($"");
            Log($"");
            Log($"");
            Log($"---------------------------------------------------------------------------------------------------");
            Log($"Google calendar reader - Oliver Abraham - Version {AppVersion.Version.VERSION}");
            Log($"---------------------------------------------------------------------------------------------------");
        }

        private static void Divider()
        {
            Log($"---------------------------------------------------------------------------------------------------");
            Log($"");
            Log($"");
            Log($"");
        }

        private static bool InitWorker()
        {
            _logic = new CalendarReaderLogic();
            _logic.Logger = (message) => Log(message);
            var success = _logic.ReadConfiguration();
            if (success) _logic.LogConfiguration();
            return success;
        }

        private static void InitScheduler()
        {
            Log("Starting periodic reading. Press Ctrl-C to stop.");

            _scheduler = new Scheduler()
                .UseIntervalMinutes(_logic.UpdateIntervalInMinutes)
                .UseFirstStartRightNow()
                .UseAction(() => _logic.ReadCalendar())
                .Start();
        }

        private static void InitAndStartWebServer(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            app.MapGet("/", () => GetWholeLog());

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            app.Run();
            _scheduler.Stop();
            Console.WriteLine("Program ended");
        }
        #endregion



        #region ------------- Simple logger for website display -----------------------------------
        private static void Log(string message)
        {
            _logger.Info(message);
            WebsiteLogger(message);
        }

        private static void WebsiteLogger(string message)
        {
            var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {message}";
            _log.Add(line);
            //Console.WriteLine(line);

            // automatic purge
            int maxLines = (_logic is not null) ? _logic.MaxLogMessagesInUI : 100;
            while (_log.Count > maxLines)
                _log.RemoveAt(0);
        }

        private static string GetWholeLog()
        {
            return string.Join("\n", _log);
        }
        #endregion



        #region ------------- NLog logger ---------------------------------------------------------
        private static void InitLogging()
        {
			// ATTENTION: Go to Properties of nlog.config and set it to "copy if newer", to have it in output directory!
            _logger = LogManager.LoadConfiguration("nlog.config").GetCurrentClassLogger();
        }
        #endregion
    }
}
