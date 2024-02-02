
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;

public class DigitalClock : UdonSharpBehaviour
{
    public Material yearMaterial1;
    public Material yearMaterial2;
    public Material yearMaterial3;
    public Material yearMaterial4;
    public Material monthMaterial1;
    public Material monthMaterial2;
    public Material dayMaterial1;
    public Material dayMaterial2;
    public Material hourMaterial1;
    public Material hourMaterial2;
    public Material minuteMaterial1;
    public Material minuteMaterial2;
    public Material colonMaterial;

    private DateTime lastUpdateTime;
    private bool lastColonState = false;
    private float blinkState = 1.0f;

    private void Update()
    {
        DateTime currentTime = DateTime.Now;

        if (currentTime.Minute != lastUpdateTime.Minute)
        {
            UpdateSegmentStates(currentTime.Year / 1000 % 10, yearMaterial1);
            UpdateSegmentStates(currentTime.Year / 100 % 10, yearMaterial2);
            UpdateSegmentStates(currentTime.Year / 10 % 10, yearMaterial3);
            UpdateSegmentStates(currentTime.Year % 10, yearMaterial4);

            UpdateSegmentStates(currentTime.Month / 10, monthMaterial1);
            UpdateSegmentStates(currentTime.Month % 10, monthMaterial2);

            UpdateSegmentStates(currentTime.Day / 10, dayMaterial1);
            UpdateSegmentStates(currentTime.Day % 10, dayMaterial2);

            UpdateSegmentStates(currentTime.Hour / 10, hourMaterial1);
            UpdateSegmentStates(currentTime.Hour % 10, hourMaterial2);

            UpdateSegmentStates(currentTime.Minute / 10, minuteMaterial1);
            UpdateSegmentStates(currentTime.Minute % 10, minuteMaterial2);

            lastUpdateTime = currentTime;
        }

        bool shouldShowColon = currentTime.Millisecond < 500;

        if (shouldShowColon != lastColonState)
        {
            blinkState = shouldShowColon ? 1.0f : 0.0f;
            colonMaterial.SetFloat("_Udon_BlinkState", blinkState);
            lastColonState = shouldShowColon;
        }
    }

    private void UpdateSegmentStates(int digit, Material material)
    {
        bool[] segmentStates = GetSegmentStatesForDigit(digit);

        material.SetFloat("_Udon_TopSegment", segmentStates[0] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_TopRightSegment", segmentStates[1] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_BottomRightSegment", segmentStates[2] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_BottomSegment", segmentStates[3] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_BottomLeftSegment", segmentStates[4] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_TopLeftSegment", segmentStates[5] ? 1.0f : 0.0f);
        material.SetFloat("_Udon_MiddleSegment", segmentStates[6] ? 1.0f : 0.0f);
    }

    private bool[] GetSegmentStatesForDigit(int digit)
    {
        switch (digit)
        {
            case 0:
                return new bool[] { true, true, true, true, true, true, false };
            case 1:
                return new bool[] { false, true, true, false, false, false, false };
            case 2:
                return new bool[] { true, true, false, true, true, false, true };
            case 3:
                return new bool[] { true, true, true, true, false, false, true };
            case 4:
                return new bool[] { false, true, true, false, false, true, true };
            case 5:
                return new bool[] { true, false, true, true, false, true, true };
            case 6:
                return new bool[] { true, false, true, true, true, true, true };
            case 7:
                return new bool[] { true, true, true, false, false, false, false };
            case 8:
                return new bool[] { true, true, true, true, true, true, true };
            case 9:
                return new bool[] { true, true, true, true, false, true, true };
            default:
                return new bool[] { false, false, false, false, false, false, false };
        }
    }
}
