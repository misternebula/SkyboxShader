Shader "Unlit/SkyboxShader"
{
	Properties
	{
		_BaseSkyColor("Base Sky Color", Color) = (0, 0.372549, 0.6117647, 1)
		_HorizonColor("Horizon Color", Color) = (1, 0.772549, 0.4078431, 0)

		_SunSize("Sun Size", Float) = 0.04

		[HDR]_MoonColor("Moon Color", Color) = (5.992157, 5.992157, 5.992157, 0)
		_MoonSize("Moon Size", Float) = 0.04

		_StarScale("Star Scale", Float) = 10
		_StarSmoothstepMin("Star Smoothstep Min", Float) = 0
		_StarSmoothstepMax("Star Smoothstep Max", Float) = 0

		_StarColor1("Star Color 1", Color) = (1, 0.0666666667, 0, 1)
		_StarColor2("Star Color 2", Color) = (1, 0.796078431, 0.062745098, 1)
		_StarColor2Value("Adjustment value for Star Color 2", float) = 0.19
		_StarColor3("Star Color 3", Color) = (1, 1, 1, 1)
		_StarColor3Value("Adjustment value for Star Color 3", float) = 0.65
		_StarColor4("Star Color 4", Color) = (0, 0.521568627, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "NoiseShader/HLSL/SimplexNoise3D.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 objPos : TEXCOORD0;
				float4 worldPos : TEXCOORD1;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.objPos = v.vertex;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				return o;
			}

			float4 _BaseSkyColor;
			float4 _HorizonColor;
			float4 _SunColor;
			float _SunSize;
			float _SunSmooth;
			float4 _MoonColor;
			float _MoonSize;
			float _MoonSmooth;

			float _StarScale;
			float _StarSmoothstepMin;
			float _StarSmoothstepMax;
			float4 _StarColor1;
			float4 _StarColor2;
			float _StarColor2Value;
			float4 _StarColor3;
			float _StarColor3Value;
			float4 _StarColor4;

			float3 _SunDirection;
			float3 _MoonDirection;

			float simplexNoise3D(float3 samplingCoords, float scale)
			{
				return (snoise(samplingCoords * scale) + 1) / 2;
			}

			float remap(float In, float2 InMinMax, float2 OutMinMax)
			{
				return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float3 normViewDir = normalize(WorldSpaceViewDir(i.worldPos));

				// stars

				float starNoise = acos(simplexNoise3D(normViewDir, _StarScale));
				float starValue = 1 - (smoothstep(_StarSmoothstepMin, _StarSmoothstepMax, starNoise));

				float3 offsetSamplingCoords = normViewDir + float3(100, 100, 100);
				float starColorNoise = simplexNoise3D(offsetSamplingCoords, _StarScale);

				float4 starColor = float4(0, 0, 0, 1);
				if (starColorNoise <= _StarColor2Value)
				{
					starColor = lerp(_StarColor1, _StarColor2, starColorNoise / _StarColor2Value);
				}
				else if (starColorNoise <= _StarColor3Value)
				{
					float remappedNoiseValue = remap(starColorNoise, float2(_StarColor2Value, _StarColor3Value), float2(0, 1));
					starColor = lerp(_StarColor2, _StarColor3, remappedNoiseValue);
				}
				else
				{
					float remappedNoiseValue = remap(starColorNoise, float2(_StarColor3Value, 1), float2(0, 1));
					starColor = lerp(_StarColor3, _StarColor4, remappedNoiseValue);
				}

				float4 stars = starValue * starColor;

				// base sky color
				float yPosition = 6 * i.objPos.y;

				if (yPosition >= 1)
				{
					yPosition = pow(yPosition, 0.2);
				}
				
				float clampedYPosition = clamp(yPosition, 0, 1);
				float4 baseColor = lerp(_HorizonColor, _BaseSkyColor, clampedYPosition);

				// sun
				float dotProduct = dot(-normViewDir, normalize(_SunDirection));
				float4 sunOverlay = float4(((1 - step(_SunSize, acos(dotProduct))) * _SunColor).xxx, 1);

				if (all(sunOverlay == float4(0, 0, 0, 1)))
				{
					sunOverlay.a = 0;
				}

				// moon
				dotProduct = dot(-normViewDir, normalize(_MoonDirection));
				float stepValue = 1 - step(_MoonSize, acos(dotProduct));
				float4 moonOverlay = float4(stepValue.xxx, 0);

				if (stepValue == 1)
				{
					moonOverlay.a = 1;
				}

				if (all(moonOverlay != float4(0, 0, 0, 0)))
				{
					// generate fake normals for the moon based on distance from the center
					float3 moonToCamera = normalize(-_MoonDirection);
					float3 moonToFrag = normalize(-normViewDir - normalize(_MoonDirection));
					float angle = acos(dotProduct) / _MoonSize;
					float3 lerpedVector = lerp(moonToCamera, moonToFrag, angle);

					// fake lighting
					moonOverlay.xyz = clamp(dot(_SunDirection, lerpedVector), 0, 1);
				}

				// blend based on alpha so that the moon can cover the sun
				float4 blendedSunAndMoon = float4(lerp(sunOverlay, moonOverlay, moonOverlay.a).xyz, clamp(moonOverlay.a + sunOverlay.a, 0, 1));

				// blend based on alpha so that the moon can cover the stars
				float4 blendedStarsSunMoon = float4(lerp(stars, blendedSunAndMoon, blendedSunAndMoon.a).xyz, 1);

				return blendedStarsSunMoon + baseColor;
			}
			ENDCG
		}
	}
}
