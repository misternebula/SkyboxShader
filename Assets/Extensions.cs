using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

internal static class Extensions
{
	private const byte MAX_BYTE_FOR_OVEREXPOSED_COLOR = 191;

	public static Color32 HdrToRgb(this Color hdrColor)
	{
		Color32 baseLinearColor = hdrColor;

		var maxColorComponent = hdrColor.maxColorComponent;

		if (maxColorComponent == 0f || (maxColorComponent <= 1f && maxColorComponent >= (1 / 255f)))
		{
			baseLinearColor.r = (byte)Mathf.RoundToInt(hdrColor.r * 255f);
			baseLinearColor.g = (byte)Mathf.RoundToInt(hdrColor.g * 255f);
			baseLinearColor.b = (byte)Mathf.RoundToInt(hdrColor.b * 255f);
		}
		else
		{
			var scaleFactor = MAX_BYTE_FOR_OVEREXPOSED_COLOR / maxColorComponent;

			baseLinearColor.r = Math.Min(MAX_BYTE_FOR_OVEREXPOSED_COLOR, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.r));
			baseLinearColor.g = Math.Min(MAX_BYTE_FOR_OVEREXPOSED_COLOR, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.g));
			baseLinearColor.b = Math.Min(MAX_BYTE_FOR_OVEREXPOSED_COLOR, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.b));
		}

		return baseLinearColor;
	}
}
