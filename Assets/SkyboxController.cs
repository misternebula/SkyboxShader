using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxController : MonoBehaviour
{
	[Range(0.00f, 24.00f)]
	public float TimeOfDay;

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
		var (elevation, azimuth) = GetSolarElevationAzimuth(TimeOfDay);

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

	private (double elevation, double azimuth) GetSolarElevationAzimuth(float timeOfDay)
	{
		// Based on R code from https://stackoverflow.com/questions/8708048, which is based on
		// Michalsky, J.J. 1988. The Astronomical Almanac's algorithm for approximate solar position (1950-2050). Solar Energy. 40(3):227-235.

		// simplified down to constants for the date of 1st August 2022, at Lat:51.5 Long:0

		const double TAU = Math.PI * 2;

		// get julian date
		var time = 8247.5 + (timeOfDay / 24);

		// Celestial coordinates
		var rightAscension = 2.3;
		var declination = 17.85 * Mathf.Deg2Rad;

		// local coords
		// greenwich mean siderial time
		var gmst = 6.697375 + (.0657098242 * time) + timeOfDay;
		gmst %= 24;
		if (gmst < 0)
		{
			gmst += 24;
		}

		gmst = gmst * 15 * Mathf.Deg2Rad;

		// hour angle
		var hourAngle = gmst - rightAscension;
		if (hourAngle < -Math.PI)
		{
			hourAngle += TAU;
		}

		if (hourAngle > Math.PI)
		{
			hourAngle -= TAU;
		}

		// azimuth and elevation
		var elevation = Math.Asin((Math.Sin(declination) * 0.782608) + (Math.Cos(declination) * 0.622515 * Math.Cos(hourAngle)));
		var azimuth = Math.Asin(-Math.Cos(declination) * Math.Sin(hourAngle) / Math.Cos(elevation));

		var cosAzPos = 0 <= Math.Sin(declination) - (Math.Sin(elevation) * 0.782608);
		var sinAzNeg = Math.Sin(azimuth) < 0;
		if (cosAzPos && sinAzNeg)
		{
			azimuth += TAU;
		}

		if (!cosAzPos)
		{
			azimuth = Math.PI - azimuth;
		}

		return (elevation, azimuth);
	}

	private void OnDrawGizmosSelected()
	{
		var listOfPoints = new List<Vector3>();
		var listOfColors = new List<Color>();
		for (var i = 0; i <= 24; i++)
		{
			var (elevation, azimuth) = GetSolarElevationAzimuth(i);

			var sunDirection = new Vector3(
			(float)Math.Cos(elevation) * (float)Math.Cos(azimuth),
			(float)Math.Sin(elevation),
			(float)Math.Cos(elevation) * (float)Math.Sin(azimuth)) * 10000;

			listOfPoints.Add(transform.position + sunDirection);
			listOfColors.Add(SunGradient.Evaluate(GetRepeatEvaluate(elevation)).HdrToRgb());
		}

		for (int i = 0; i < listOfPoints.Count - 1; i++)
		{
			Gizmos.color = listOfColors[i];
			Gizmos.DrawLine(listOfPoints[i], listOfPoints[i + 1]);
		}

		var (currentEl, currentAz) = GetSolarElevationAzimuth(TimeOfDay);
		var currentSunDirection = new Vector3(
			(float)Math.Cos(currentEl) * (float)Math.Cos(currentAz),
			(float)Math.Sin(currentEl),
			(float)Math.Cos(currentEl) * (float)Math.Sin(currentAz)) * 10000;
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(transform.position, transform.position + currentSunDirection);
	}
}
