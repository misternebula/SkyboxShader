using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxController : MonoBehaviour
{
	[Range(1950, 2050)]
	public int YEAR;

	[Range(1, 12)]
	public int MONTH;

	[Range(1, 31)]
	public int DAY;

	[Range(0, 23)]
	public int HOUR;

	[Range(0, 60)]
	public int MIN;

	[Range(0, 60)]
	public int SEC;

	[Header("Sun")]
	public Transform SunPivot;
	public Light SunLight;
	[GradientUsage(true)]
	public Gradient SunGradient;
	public AnimationCurve SunIntensity;

	[Header("Moon")]
	public Light MoonLight;
	public AnimationCurve MoonIntensity;

	[Header("Skybox")]
	public Material SkyboxMaterial;
	public Gradient SkyboxHorizonGradient;
	public Gradient SkyboxSkyGradient;

	[Header("Global Lighting")]
	public AnimationCurve AmbientIntensityGradient;

	public void OnValidate()
	{
		UpdateSettings();
	}

	private void UpdateSettings()
	{
		var (elevation, azimuth) = GetSolarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);

		SkyboxMaterial.SetColor("_HorizonColor", SkyboxHorizonGradient.Evaluate(GetRepeatEvaluate(elevation)));
		SkyboxMaterial.SetColor("_BaseSkyColor", SkyboxSkyGradient.Evaluate(GetRepeatEvaluate(elevation)));
		var sunColor = SunGradient.Evaluate(GetRepeatEvaluate(elevation));
		var sunColorNonHDR = sunColor.HdrToRgb();
		Shader.SetGlobalColor("_SunColor", sunColor);
		SunLight.color = sunColorNonHDR;
		SunLight.intensity = SunIntensity.Evaluate(GetRepeatEvaluate(elevation));
		MoonLight.intensity = MoonIntensity.Evaluate(GetRepeatEvaluate(elevation));

		SunLight.enabled = !Mathf.Approximately(SunLight.intensity, 0f);
		MoonLight.enabled = !Mathf.Approximately(MoonLight.intensity, 0f);

		var sunDirection = new Vector3(
			(float)Math.Cos(elevation) * (float)Math.Cos(azimuth),
			(float)Math.Sin(elevation),
			(float)Math.Cos(elevation) * (float)Math.Sin(azimuth));
		SunPivot.forward = -sunDirection;
		Shader.SetGlobalVector("_SunDirection", sunDirection);

		var intensity = AmbientIntensityGradient.Evaluate(GetRepeatEvaluate(elevation));
		RenderSettings.ambientIntensity = intensity;
		RenderSettings.reflectionIntensity = intensity;
		RenderSettings.fogColor = SkyboxHorizonGradient.Evaluate(GetRepeatEvaluate(elevation));
	}

	public float GetRepeatEvaluate(double elevation)
	{
		var val = (float)((elevation * Mathf.Rad2Deg) + 90f);
		return 1 - (val / 180);
	}

	private (double elevation, double azimuth) GetSolarElevationAzimuth(int year, int month, int day, int hour, int min, int sec, double lat, double longitude)
	{
		// Based on R code from https://stackoverflow.com/questions/8708048, which is based on
		// Michalsky, J.J. 1988. The Astronomical Almanac's algorithm for approximate solar position (1950-2050). Solar Energy. 40(3):227-235.

		const double twopi = Math.PI * 2;
		const double deg2rad = Math.PI / 180;

		// Get day of year
		var monthdays = new int[] { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30 };
		for (var i = 1; i <= month; i++)
		{
			day += monthdays[i - 1];
		}
		var leapdays = year % 4 == 0 & (year % 400 == 0 | year % 100 != 0) & day >= 60 & !(year == 2 & day == 60);
		if (leapdays)
		{
			day++;
		}

		// Get Julian date - 2400000
		var hourFRAC = hour + (min / 60f) + (sec / 36000f); // hour plus fraction
		var delta = year - 1949;
		var leap = Math.Truncate(delta / 4f); // former leapyears
		var jd = 32916.5f + (delta * 365) + leap + day + (hourFRAC / 24);

		// The input to the Atronomer's almanach is the difference between
		// the Julian date and JD 2451545.0 (noon, 1 January 2000)
		var time = jd - 51545;

		// Ecliptic coordinates

		// Mean longitude
		var mnlong = 280.460 + (0.9856474 * time);
		mnlong %= 360;
		if (mnlong < 0)
		{
			mnlong += 360;
		}

		// Mean anomoly
		var mnanom = 357.528 + (0.9856003 * time);
		mnanom %= 360;
		if (mnanom < 0)
		{
			mnanom += 360;
		}
		mnanom *= deg2rad;

		// Ecliptic longitude and obliquity of ecliptic
		var eclong = mnlong + (1.915 * Math.Sin(mnanom)) + (0.020 * Math.Sin(2 * mnanom));
		eclong %= 360;
		if (eclong < 0)
		{
			eclong += 360;
		}
		var oblqec = 23.439 - (0.0000004 * time);
		eclong *= deg2rad;
		oblqec *= deg2rad;

		// Celestial coordinates
		// Right ascension and declination
		var num = Math.Cos(oblqec) * Math.Sin(eclong);
		var den = Math.Cos(eclong);
		var ra = Math.Atan(num / den);
		if (den < 0)
		{
			ra += Math.PI;
		}
		if (den >= 0 && num < 0)
		{
			ra += twopi;
		}
		var dec = Math.Asin(Math.Sin(oblqec) * Math.Sin(eclong));

		// Local coordinates
		// Greenwich mean sidereal time
		var gmst = 6.697375 + (.0657098242 * time) + hourFRAC;
		gmst %= 24;
		if (gmst < 0)
		{
			gmst += 24;
		}

		// Local mean sidereal time
		var lmst = gmst + (longitude / 15);
		lmst %= 24;
		if (lmst < 0)
		{
			lmst += 24;
		}
		lmst = lmst * 15 * deg2rad;

		// Hour angle
		var ha = lmst - ra;
		if (ha < -Math.PI)
		{
			ha += twopi;
		}
		if (ha > Math.PI)
		{
			ha -= twopi;
		}

		// Latitude to radians
		lat *= deg2rad;

		// Azimuth and elevation
		var el = Math.Asin((Math.Sin(dec) * Math.Sin(lat)) + (Math.Cos(dec) * Math.Cos(lat) * Math.Cos(ha)));
		var az = Math.Asin(-Math.Cos(dec) * Math.Sin(ha) / Math.Cos(el));

		// For logic and names, see Spencer, J.W. 1989. Solar Energy. 42(4):353
		var cosAzPos = 0 <= Math.Sin(dec) - (Math.Sin(el) * Math.Sin(lat));
		var sinAzNeg = Math.Sin(az) < 0;

		if (cosAzPos && sinAzNeg)
		{
			az += twopi;
		}

		if (!cosAzPos)
		{
			az = Math.PI - az;
		}

		//el = el / deg2rad;
		//az = az / deg2rad;

		return (el, az);
	}

	private void OnDrawGizmosSelected()
	{
		var listOfPoints = new List<Vector3>();
		var listOfColors = new List<Color>();
		for (var i = 0; i <= 24; i++)
		{
			for (var j = 1; j < 60; j++)
			{
				var (elevation, azimuth) = GetSolarElevationAzimuth(YEAR, MONTH, DAY, i, j, SEC, 51.5, 0);

				var sunDirection = new Vector3(
				(float)Math.Cos(elevation) * (float)Math.Cos(azimuth),
				(float)Math.Sin(elevation),
				(float)Math.Cos(elevation) * (float)Math.Sin(azimuth)) * 10000;

				listOfPoints.Add(transform.position + sunDirection);
				listOfColors.Add(SunGradient.Evaluate(GetRepeatEvaluate(elevation)).HdrToRgb());
			}
		}

		for (int i = 0; i < listOfPoints.Count - 1; i++)
		{
			Gizmos.color = listOfColors[i];
			Gizmos.DrawLine(listOfPoints[i], listOfPoints[i + 1]);
		}

		var (currentEl, currentAz) = GetSolarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);
		var currentSunDirection = new Vector3(
			(float)Math.Cos(currentEl) * (float)Math.Cos(currentAz),
			(float)Math.Sin(currentEl),
			(float)Math.Cos(currentEl) * (float)Math.Sin(currentAz)) * 10000;
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(transform.position, transform.position + currentSunDirection);
	}
}
