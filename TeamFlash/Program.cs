﻿using System;
using System.Collections.Generic;
using Mono.Options;
using TeamFlash.Delcom;
using Monitor = TeamFlash.Delcom.Monitor;

namespace TeamFlash
{
    class Program
    {
        static void Main(string[] args)
        {
			var help = false;
			var serverUrl = string.Empty;
			var username = string.Empty;
			var password = string.Empty;
			var guestAuth = false;
			var specificProject = string.Empty;
            bool failOnFirstFailed = false;
            string buildLies = string.Empty;
            double pollInterval = 60000;

            var options = new OptionSet()
					.Add("?|help|h", "Output options", option => help = option != null)
					.Add("s=|url=|server=", "TeamCity URL", option => serverUrl = option)
					.Add("u|user=|username=", "Username", option => username = option)
					.Add("p|password=","Password", option => password = option)
					.Add("g|guest|guestauth", "Connect using anonymous guestAuth", option => guestAuth = option != null)
					.Add("sp|specificproject=","Constrain to a specific project", option => specificProject = option)
                    .Add("f|failonfirstfailed", "Check until finding the first failed", option => failOnFirstFailed = option != null)
                    .Add("l|lies=","Lie for these builds, say they are green", option => buildLies = option)
                    .Add("i|interval","Time interval in seconds to poll server.", option => pollInterval = option != null ? Convert.ToDouble(option) : 60000);

			try
			{
				options.Parse(args);
			}
			catch (OptionException)
			{
				OutputFailureAndExit(options, "Incorrect arguments, usage is: ");
			}

			if (help)
			{
				Console.WriteLine(options);
				Environment.Exit(0);
			}

			if (string.IsNullOrEmpty(serverUrl))
				OutputFailureAndExit(options, "Must have a Server URL provided");

			if (!guestAuth && string.IsNullOrEmpty(username))
				OutputFailureAndExit(options, "Either provide username/password or use guestAuth = true");

            var monitor = new Monitor();
            monitor.TurnOffLights();
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => monitor.TurnOffLights();

            monitor.TestLights();
            monitor.Disco(2);
            monitor.TurnOffLights();
            TeamCityBuildMonitor buildMonitor = null;
            try
            {
                var lies = new List<String>(buildLies.ToLowerInvariant().Split(';'));
                ITeamCityApi api = new TeamCityApi(serverUrl);
                buildMonitor = new TeamCityBuildMonitor(api, specificProject, failOnFirstFailed, lies, pollInterval);
                buildMonitor.CheckFailed += (sender, eventArgs) =>
                    {
                        monitor.TurnOnFailLight();
                        Console.WriteLine(DateTime.Now.ToShortTimeString() + " Failed");
                    };
                buildMonitor.BuildChecked += (sender, eventArgs) => monitor.Blink();
                var blinkInterval = 30;
                buildMonitor.BuildPaused += (sender, eventArgs) => monitor.BlinkThenRevert(LedColour.Yellow,blinkInterval);
                buildMonitor.BuildSkipped += (sender, eventArgs) => monitor.BlinkThenRevert(LedColour.Purple,blinkInterval);
                buildMonitor.BuildSuccess += (sender, eventArgs) => monitor.BlinkThenRevert(LedColour.Green, blinkInterval);
                buildMonitor.BuildFail += (sender, eventArgs) => monitor.BlinkThenRevert(LedColour.Red, blinkInterval);
                buildMonitor.BuildUnknown += (sender, eventArgs) => monitor.BlinkThenRevert(LedColour.Yellow, blinkInterval);
                buildMonitor.CheckSuccessfull += (sender, eventArgs) =>
                    {
                        monitor.TurnOnSuccessLight();
                        Console.WriteLine(DateTime.Now.ToShortTimeString() + " Passed");
                    };
                buildMonitor.ServerCheckException += (sender, eventArgs) => Console.WriteLine(DateTime.Now.ToShortTimeString() + " Server unavailable");
                buildMonitor.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            while (!Console.KeyAvailable)
            {
            }
            if (buildMonitor != null) buildMonitor.Stop();
            monitor.TurnOffLights();
        }

        static void OutputFailureAndExit(OptionSet options, string message)
		{
			Console.WriteLine(message);
			Console.WriteLine("teamflash.exe /s[erver] VALUE /u[sername] VALUE /p[assword] VALUE /g[uestauth] /sp[ecificproject] VALUE");
			options.WriteOptionDescriptions(Console.Error);
			Environment.Exit(1);
		}
    }
}
