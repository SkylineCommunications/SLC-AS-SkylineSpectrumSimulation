/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

16/05/2024	1.0.0.1		JAY, Skyline	Initial version
****************************************************************************
*/

namespace InitializeDefaultCarriers_1
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private readonly int freqShiftOffsetMHz = 150;
		private readonly string commonSatIndex1 = "Common Satellite spectrum";
		private readonly string commonSatIndex2 = "Common Satellite spectrum_1";

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

		private void RunSafe(IEngine engine)
		{
			// this flag is needed in order to double check if new rows were created during the retry loop.
			engine.SetFlag(RunTimeFlags.NoKeyCaching);

			ScriptDummy spectrumElement = engine.GetDummy("Linked Spectrum Element");
			string mode = engine.GetScriptParam("Mode").Value;
			string indexReference = engine.GetScriptParam("IndexReference").Value;

			switch (mode)
			{
				case "Initialize":
					InitializeDefaults(engine, spectrumElement);
					break;
				case "GoodWeather":
					HelloSunshine(engine, spectrumElement);
					break;
				case "BadWeather":
					GoodbyeSunshine(engine, spectrumElement);
					break;
				case "FrequencyShiftUp":
					ShiftFrequency(engine, spectrumElement, indexReference, freqShiftOffsetMHz);
					break;
				case "FrequencyShiftDown":
					ShiftFrequency(engine, spectrumElement, indexReference, -freqShiftOffsetMHz);
					break;
				default:
					engine.GenerateInformation($"Unknown mode: {mode}");
					break;
			}
		}

		private void ShiftFrequency(IEngine engine, ScriptDummy spectrumElement, string indexReference, int freqShiftOffset)
		{
			if (!CheckIfKeysAreCreated(engine, spectrumElement))
			{
				InitializeDefaults(engine, spectrumElement);
			}

			double currentCF = Convert.ToDouble(spectrumElement.GetParameterByPrimaryKey(302 /*CF*/, indexReference));
			spectrumElement.SetParameterByPrimaryKey(352 /*CF*/, indexReference, currentCF + freqShiftOffset);
		}

		private void GoodbyeSunshine(IEngine engine, ScriptDummy spectrumElement)
		{
			if (!CheckIfKeysAreCreated(engine, spectrumElement))
			{
				InitializeDefaults(engine, spectrumElement);
			}

			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex1, 20);
			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex2, 10);
		}

		private void HelloSunshine(IEngine engine, ScriptDummy spectrumElement)
		{
			if (!CheckIfKeysAreCreated(engine, spectrumElement))
			{
				InitializeDefaults(engine, spectrumElement);
			}

			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex1, 30);
			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex2, 20);
		}

		private void InitializeDefaults(IEngine engine, ScriptDummy spectrumElement)
		{
			if (!CheckIfKeysAreCreated(engine, spectrumElement))
			{
				// if the keys are not yet created, let's create them!
				spectrumElement.SetParameter(12 /*AddDefaultPreset*/, 1 /*Add Common Satellite Spectrum*/);
				spectrumElement.SetParameter(12 /*AddDefaultPreset*/, 1 /*Add Common Satellite Spectrum*/);
				engine.Sleep(250);

				// expected rows "Common Satellite spectrum" & "Common Satellite spectrum_1" to be created.
				if (!Retry(() => CheckIfKeysAreCreated(engine, spectrumElement), TimeSpan.FromSeconds(20)))
				{
					engine.ExitFail($"InitializeDefaults failed. Expected keys are not created.");
				}
			}

			// Configure the two indexes
			spectrumElement.SetParameterByPrimaryKey(352 /*CF*/, commonSatIndex1, 11750);
			spectrumElement.SetParameterByPrimaryKey(353 /*SPAN*/, commonSatIndex1, 36);
			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex1, 30);

			spectrumElement.SetParameterByPrimaryKey(352 /*CF*/, commonSatIndex2, 11790);
			spectrumElement.SetParameterByPrimaryKey(353 /*SPAN*/, commonSatIndex2, 9);
			spectrumElement.SetParameterByPrimaryKey(354 /*AMPL*/, commonSatIndex2, 20);
		}

		private bool CheckIfKeysAreCreated(IEngine engine, ScriptDummy spectrumElement)
		{
			var keys = spectrumElement.GetTablePrimaryKeys(300 /*CarrierTable*/);
			if (keys != null)
			{
				engine.GenerateInformation($"found keys: {String.Join(", ", keys)}");
				if (keys.Contains(commonSatIndex1) && keys.Contains(commonSatIndex2))
				{
					return true;
				}
			}

			return false;
		}

		private bool Retry(Func<bool> func, TimeSpan timeout)
		{
			bool success = false;

			Stopwatch sw = new Stopwatch();
			sw.Start();

			do
			{
				success = func();
				if (!success)
				{
					System.Threading.Thread.Sleep(250);
				}
			}
			while (!success && sw.Elapsed <= timeout);

			return success;
		}
	}
}