using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

	[Range(0, 59)]
	public int MIN;

	[Range(0, 59)]
	public int SEC;

	public bool AutoChange;
	public double ChangeAmount;

	[Header("Sun")]
	public Transform Sun;
	[GradientUsage(true)]
	public Gradient SunGradient;
	public AnimationCurve SunIntensity;

	[Header("Moon")]
	public Transform Moon;
	public AnimationCurve MoonIntensity;

	[Header("Skybox")]
	public Material SkyboxMaterial;
	public Gradient SkyboxHorizonGradient;
	public Gradient SkyboxSkyGradient;

	[Header("Global Lighting")]
	public AnimationCurve AmbientIntensityGradient;

	private Light _sunLight;
	private Light _moonLight;

	public void OnValidate()
	{
		UpdateSettings();
	}

	private double _secondsCounter;
	private bool _inited = false;
	private DateTime _startTime;

	public void FixedUpdate()
	{
		if (!AutoChange)
		{
			return;
		}

		if (!_inited)
		{
			_inited = true;
			_startTime = DateTime.Now;
			return;
		}

		_secondsCounter += ChangeAmount;

		var currentDatetime = _startTime.AddSeconds((int)_secondsCounter);

		YEAR = currentDatetime.Year;
		MONTH = currentDatetime.Month;
		DAY = currentDatetime.Day;
		HOUR = currentDatetime.Hour;
		MIN = currentDatetime.Minute;
		SEC = currentDatetime.Second;

		UpdateSettings();
	}

	private void UpdateSettings()
	{
		if (_sunLight == null)
		{
			_sunLight = Sun.GetComponent<Light>();
		}

		if (_moonLight == null)
		{
			_moonLight = Moon.GetComponent<Light>();
		}

		var (solarElevation, solarAzimuth) = GetSolarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);
		var (lunarElevation, lunarAzimuth) = GetLunarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);

		SkyboxMaterial.SetColor("_HorizonColor", SkyboxHorizonGradient.Evaluate(GetRepeatEvaluate(solarElevation)));
		SkyboxMaterial.SetColor("_BaseSkyColor", SkyboxSkyGradient.Evaluate(GetRepeatEvaluate(solarElevation)));
		var sunColor = SunGradient.Evaluate(GetRepeatEvaluate(solarElevation));
		var sunColorNonHDR = sunColor.HdrToRgb();
		Shader.SetGlobalColor("_SunColor", sunColor);
		_sunLight.color = sunColorNonHDR;
		_sunLight.intensity = SunIntensity.Evaluate(GetRepeatEvaluate(solarElevation));
		_moonLight.intensity = MoonIntensity.Evaluate(GetRepeatEvaluate(solarElevation));

		_sunLight.enabled = !Mathf.Approximately(_sunLight.intensity, 0f);
		_moonLight.enabled = !Mathf.Approximately(_moonLight.intensity, 0f);

		var sunDirection = new Vector3(
			(float)Math.Cos(solarElevation) * (float)Math.Cos(-solarAzimuth),
			(float)Math.Sin(solarElevation),
			(float)Math.Cos(solarElevation) * (float)Math.Sin(-solarAzimuth));

		Sun.forward = -sunDirection;

		Shader.SetGlobalVector("_SunDirection", sunDirection);

		var moonDirection = new Vector3(
			(float)Math.Cos(lunarElevation) * (float)Math.Cos(-lunarAzimuth),
			(float)Math.Sin(lunarElevation),
			(float)Math.Cos(lunarElevation) * (float)Math.Sin(-lunarAzimuth));

		Moon.forward = -moonDirection;

		Shader.SetGlobalVector("_MoonDirection", moonDirection);

		var intensity = AmbientIntensityGradient.Evaluate(GetRepeatEvaluate(solarElevation));
		RenderSettings.ambientIntensity = intensity;
		RenderSettings.reflectionIntensity = intensity;
		RenderSettings.fogColor = SkyboxHorizonGradient.Evaluate(GetRepeatEvaluate(solarElevation));
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
		var hourFRAC = hour + (min / 60f) + (sec / 3600f); // hour plus fraction
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

	private (double elevation, double azimuth) GetLunarElevationAzimuth(int year, int month, int day, int hour, int min, int sec, double Lat, double Lon)
	{
		var EarthRadEq = 6378.1370;

		var jd = juliandate(year, month, day, hour, min, sec);

		var d = jd - 2451543.5;

		var N = 125.1228 - 0.0529538083 * d;
		var i = 5.1454;
		var w = 318.0634 + 0.1643573223 * d;
		var a = 60.2666;
		var e = 0.054900;
		var M = (115.3654 + 13.0649929509 * d) % 360;

		var LMoon = (N + w + M) % 360;
		var FMoon = (LMoon - N) % 360;

		var wSun = (282.9404 + 4.70935E-5 * d) % 360;
		var MSun = (356.0470 + 0.9856002585 * d) % 360;
		var LSun = mod(wSun + MSun, 360);

		var DMoon = LMoon - LSun;

		var LunarPLon = new double[]
		{
			-1.274 * sin((M - 2 * DMoon) * (pi/180)),
			.658 * sin(2 * DMoon * (pi/180)),
			-0.186 * sin(MSun * (pi/180)),
			-0.059 * sin((2 * M-2 * DMoon) * (pi/180)),
			-0.057 * sin((M-2 * DMoon + MSun) * (pi/180)),
			.053 * sin((M+2 * DMoon) * (pi/180)),
			.046 * sin((2 * DMoon-MSun) * (pi/180)),
			.041 * sin((M-MSun) * (pi/180)),
			-0.035 * sin(DMoon * (pi/180)),          
			-0.031 * sin((M+MSun) * (pi/180)),
			-0.015 * sin((2 * FMoon-2 * DMoon) * (pi/180)),
			.011 * sin((M-4 * DMoon) * (pi/180))
		};

		var LunarPLat = new double[]
		{
			-0.173 * sin((FMoon-2 * DMoon) * (pi/180)),
			-0.055 * sin((M-FMoon-2 * DMoon) * (pi/180)),
			-0.046 * sin((M+FMoon-2 * DMoon) * (pi/180)),
			+0.033 * sin((FMoon+2 * DMoon) * (pi/180)),
			+0.017 * sin((2 * M+FMoon) * (pi/180))
		};

		var LunarPDist = new double[]
		{
			-0.58*cos((M-2*DMoon)*(pi/180)),
			-0.46*cos(2*DMoon*(pi/180))
		};
		
		var E0 = M + (180/ pi)* e* sin(M* (pi / 180))* (1 + e* cos(M* (pi / 180)));
		var E1 = E0 - (E0 - (180 / pi)* e* sin(E0* (pi / 180)) - M)/ (1 - e * cos(E0* (pi / 180)));

		while (E1 - E0 > .000005)
		{
			E0 = E1;
			E1 = E0 - (E0 - (180 / pi)* e* sin(E0* (pi / 180)) - M)/ (1 - e * cos(E0* (pi / 180)));
		}
		var E = E1;

		var x = a* (cos(E* (pi / 180)) - e);
		var y = a* sqrt(1 - e* e)* sin(E* (pi / 180));

		var r = sqrt(x* x + y* y);
		var v = atan2(y* (pi / 180), x* (pi / 180))* (180 / pi);

		var xeclip = r *  (cos(N *  (pi / 180)) *  cos((v + w) *  (pi / 180)) - sin(N *  (pi / 180)) *  sin((v + w) *  (pi / 180)) *  cos(i *  (pi / 180)));
		var yeclip = r *  (sin(N *  (pi / 180)) *  cos((v + w) *  (pi / 180)) + cos(N *  (pi / 180)) * sin(((v + w) *  (pi / 180))) * cos(i *  (pi / 180)));
		var zeclip = r *  sin((v + w) *  (pi / 180)) *  sin(i *  (pi / 180));

		var (eLon, eLat, eDist) = cart2sph(xeclip, yeclip, zeclip);

		(xeclip, yeclip, zeclip) = sph2cart(
			eLon + sum(LunarPLon) * (pi / 180),
			eLat + sum(LunarPLat) * (pi / 180),
			eDist + sum(LunarPDist));

		var T = (jd - 2451545.0) / 36525.0;

		var Obl = 23.439291 - 0.0130042 * T - 0.00000016 * T * T + 0.000000504 * T * T * T;
		Obl = Obl * (pi / 180);
		var RotM = new Matrix(
			1, 0, 0,
			0, cos(Obl), -sin(Obl),
			0, sin(Obl), cos(Obl));

		var sol = (RotM * new Vector(xeclip, yeclip, zeclip)) * EarthRadEq;

		var (xsl, ysl, zsl) = sph2cart(Lon * (pi / 180), Lat * (pi / 180), EarthRadEq);

		var (RA, delta, _) = cart2sph(sol.x, sol.y, sol.z);
		delta = delta * (180 / pi);
		RA = RA * (180 / pi);

		var J2000 = jd - 2451545.0;
		var UTH = hour + (min / 60.0) + (sec / 3600.0);

		var LST = mod(100.46 + 0.985647 * J2000 + Lon + 15 * UTH, 360);

		var HA = LST - RA;

		var h = asin(sin(delta * (pi / 180)) * sin(Lat * (pi / 180)) + cos(delta * (pi / 180)) * cos(Lat * (pi / 180)) * cos(HA * (pi / 180))) * (180 / pi);
		var Az = acos((sin(delta * (pi / 180)) - sin(h * (pi / 180)) * sin(Lat * (pi / 180))) / (cos(h * (pi / 180)) * cos(Lat * (pi / 180)))) * (180 / pi);

		if (sin(HA *  (pi / 180)) >= 0)
		{
			Az = 360 - Az;
		}

		var horParal = 8.794 / (r * 6379.14 / 149.59787e6);
		var p = asin(cos(h * (pi / 180)) * sin((horParal / 3600) * (pi / 180))) * (180 / pi);
		h = h - p;

		return (h * Mathf.Deg2Rad, Az * Mathf.Deg2Rad);
	}

	private double juliandate(int year, int month, int day, int hour, int min, int sec)
	{
		double floor(double x) => Math.Floor(x);

		/*for (var k = 1; k <= 1; k += -1)
		{
			if (month <= 2)
			{
				year = year - 1;
				month = month + 12;
			}
		}*/

		return floor(365.25 * (year + 4716.0)) + floor(30.6001 * (month + 1.0)) + 2.0 -
			floor(year / 100.0) + floor(floor(year / 100.0) / 4.0) + day - 1524.5 +
			(hour + min / 60.0 + sec / 3600.0) / 24.0;
	}

	(double azimuth, double elevation, double r) cart2sph(double x, double y, double z)
	{
		var azimuth = atan2(y, x);
		var elevation = atan2(z, sqrt(Math.Pow(x, 2) + Math.Pow(y, 2)));
		var r = sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
		return (azimuth, elevation, r);
	}

	(double x, double y, double z) sph2cart(double azimuth, double elevation, double r)
	{
		return (r * cos(elevation) * cos(azimuth),
			r * cos(elevation) * sin(azimuth),
			r * sin(elevation));
	}

	double sum(double[] array)
	{
		return array.Sum();
	}

	private const double pi = Math.PI;

	private double sin(double x) => Math.Sin(x);
	private double cos(double x) => Math.Cos(x);
	double mod(double x, double y) => x % y;
	double sqrt(double x) => Math.Sqrt(x);
	double atan2(double x, double y) => Math.Atan2(x, y);
	double acosd(double x)
	{
		var rad = Math.Acos(x);
		return rad * Mathf.Rad2Deg;
	}
	double dot(Vector a, Vector b) => (a.x * b.x) + (a.y * b.y) + (a.z * b.z);
	double asin(double x) => Math.Asin(x);
	double acos(double x) => Math.Acos(x);

	private void OnDrawGizmosSelected()
	{
		var listOfPoints = new List<Vector3>();
		var listOfColors = new List<Color>();

		var currentDatetime = new DateTime(YEAR, MONTH, DAY, HOUR, MIN, SEC);

		for (var i = 0; i <= 24; i++)
		{
			for (var j = 1; j < 60; j++)
			{
				var (elevation, azimuth) = GetSolarElevationAzimuth(currentDatetime.Year, currentDatetime.Month, currentDatetime.Day, currentDatetime.Hour, currentDatetime.Minute, currentDatetime.Second, 51.5, 0);

				var sunDirection = new Vector3(
				(float)Math.Cos(elevation) * (float)Math.Cos(-azimuth),
				(float)Math.Sin(elevation),
				(float)Math.Cos(elevation) * (float)Math.Sin(-azimuth)) * 10000;

				listOfPoints.Add(transform.position + sunDirection);
				listOfColors.Add(SunGradient.Evaluate(GetRepeatEvaluate(elevation)).HdrToRgb());

				currentDatetime = currentDatetime.AddMinutes(1);
			}
		}

		for (int i = 0; i < listOfPoints.Count - 1; i++)
		{
			Gizmos.color = listOfColors[i];
			Gizmos.DrawLine(listOfPoints[i], listOfPoints[i + 1]);
		}

		listOfPoints = new List<Vector3>();

		currentDatetime = new DateTime(YEAR, MONTH, DAY, HOUR, MIN, SEC);

		for (var i = 0; i <= 24; i++)
		{
			var (elevation, azimuth) = GetLunarElevationAzimuth(currentDatetime.Year, currentDatetime.Month, currentDatetime.Day, currentDatetime.Hour, currentDatetime.Minute, currentDatetime.Second, 51.5, 0);

			var sunDirection = new Vector3(
			(float)Math.Cos(elevation) * (float)Math.Cos(-azimuth),
			(float)Math.Sin(elevation),
			(float)Math.Cos(elevation) * (float)Math.Sin(-azimuth)) * 10000;

			listOfPoints.Add(transform.position + sunDirection);

			currentDatetime = currentDatetime.AddHours(1);
		}

		for (int i = 0; i < listOfPoints.Count - 1; i++)
		{
			Gizmos.color = Color.white;
			Gizmos.DrawLine(listOfPoints[i], listOfPoints[i + 1]);
		}

		var (currentEl, currentAz) = GetSolarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);
		var currentSunDirection = new Vector3(
			(float)Math.Cos(currentEl) * (float)Math.Cos(-currentAz),
			(float)Math.Sin(currentEl),
			(float)Math.Cos(currentEl) * (float)Math.Sin(-currentAz)) * 10000;
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(transform.position, transform.position + currentSunDirection);

		(currentEl, currentAz) = GetLunarElevationAzimuth(YEAR, MONTH, DAY, HOUR, MIN, SEC, 51.5, 0);
		var currentLunarDirection = new Vector3(
			(float)Math.Cos(currentEl) * (float)Math.Cos(-currentAz),
			(float)Math.Sin(currentEl),
			(float)Math.Cos(currentEl) * (float)Math.Sin(-currentAz)) * 10000;
		Gizmos.color = Color.white;
		Gizmos.DrawLine(transform.position, transform.position + currentLunarDirection);
	}
}
