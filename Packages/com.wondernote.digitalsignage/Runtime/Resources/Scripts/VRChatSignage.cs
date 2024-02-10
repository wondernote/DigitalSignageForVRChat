
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using System.Collections.Generic;
using UnityEngine.UI;
using VRC.SDK3.Components;
using System;

public class VRChatSignage : UdonSharpBehaviour
{
    [Header("API Settings")]
    public VRCUrl apiUrlObject;
    public VRCUrl[] predefinedUrls;
    private float apiCallInterval = 6 * 60 * 60;

    private int currentDisplayedIndex;
    private int preIndex;
    private float timeSinceLastApiCall = 0f;
    private float timeSinceLastImageDisplay;

    [Header("Display Settings")]
    public Texture2D defaultHorizontalImage;
    public Texture2D defaultVerticalImage;
    public Texture2D wondernoteHorizontalImage;
    public Texture2D wondernoteVerticalImage;
    public Texture2D fetchErrorImage;

    public RawImage singleImagePri;
    public RawImage[] leftRightSplitImagesPri;
    public RawImage[] fourSplitImagesPri;
    public RawImage singleImageSec;
    public RawImage[] leftRightSplitImagesSec;
    public RawImage[] fourSplitImagesSec;

    public CanvasGroup singleImagePriGroup;
    public CanvasGroup leftRightSplitImagesPriGroup;
    public CanvasGroup fourSplitImagesPriGroup;
    public CanvasGroup singleImageSecGroup;
    public CanvasGroup leftRightSplitImagesSecGroup;
    public CanvasGroup fourSplitImagesSecGroup;

    private CanvasGroup currentActiveGroup;
    private CanvasGroup nextActiveGroup;
    private float fadeDuration = 1.0f;
    private float fadeTimer;
    private bool isFading;

    DataList imgIdentifiersList = new DataList();
    DataList durationList = new DataList();
    DataList patternList = new DataList();

    private DataList imagesToDownloadList = new DataList();
    private string[][] playlistUrls;

    DataList rawImgAssignmentList = new DataList();
    private DataDictionary patternCounts = new DataDictionary();

    private int playlistIndexCount;

    private VRCImageDownloader imgDownloader;
    private Texture2D[] textureCache;

    private bool hasDownloadStarted = false;
    private int downloadingImgIndex;
    private int downloadedImageCount;
    private string[] individualIdentifiersByIndex;

    private void Start()
    {
        imgDownloader = new VRCImageDownloader();
        textureCache = new Texture2D[100];
        InitializeImages();
        FetchPlaylist();
    }

    private void InitializeImages()
    {
        DisableAllImages();
        ResetRawImages();
    }

    private void DisableAllImages()
    {
        singleImagePriGroup.gameObject.SetActive(false);
        leftRightSplitImagesPriGroup.gameObject.SetActive(false);
        fourSplitImagesPriGroup.gameObject.SetActive(false);
        singleImageSecGroup.gameObject.SetActive(false);
        leftRightSplitImagesSecGroup.gameObject.SetActive(false);
        fourSplitImagesSecGroup.gameObject.SetActive(false);
    }

    private void ResetRawImages()
    {
        singleImagePri.texture = defaultHorizontalImage;
        foreach (var img in leftRightSplitImagesPri) img.texture = defaultVerticalImage;
        foreach (var img in fourSplitImagesPri) img.texture = defaultHorizontalImage;
        singleImageSec.texture = defaultHorizontalImage;
        foreach (var img in leftRightSplitImagesSec) img.texture = defaultVerticalImage;
        foreach (var img in fourSplitImagesSec) img.texture = defaultHorizontalImage;
    }

    private void FetchPlaylist()
    {
        VRCStringDownloader.LoadUrl(apiUrlObject, this.GetComponent<UdonBehaviour>());
    }

    public override void OnStringLoadSuccess(IVRCStringDownload download)
    {
        InitializeValues();

        bool isJsonParsed = ParseJson(download.Result);
        if (!isJsonParsed)
        {
            ShowErrorImage();
            return;
        }

        assignRawImgTypesByIndex();
        playlistUrls = new string[playlistIndexCount][];
        CreateImagesDownloadList();
        ClearTextureCache();

        if (!hasDownloadStarted)
        {
            DownloadNextImage();
            hasDownloadStarted = true;
        }

        ActivateAppropriateRawImage(0);
    }

    public override void OnStringLoadError(IVRCStringDownload download)
    {
        ShowErrorImage();
        Debug.LogError("プレイリストの取得に失敗しました。");
    }

    private void ShowErrorImage()
    {
        singleImagePri.texture = fetchErrorImage;
        singleImagePriGroup.alpha = 1.0f;
        singleImagePriGroup.gameObject.SetActive(true);
    }

    private void InitializeValues()
    {
        timeSinceLastImageDisplay = 0f;
        playlistIndexCount = 0;
        currentDisplayedIndex = 0;
        preIndex = 0;
        imagesToDownloadList.Clear();

        downloadingImgIndex = 0;
        downloadedImageCount = 0;

        fadeTimer = 0.0f;
        isFading = false;
    }

    private bool ParseJson(string jsonResponse)
    {
        DataToken result;
        if (VRCJson.TryDeserializeFromJson(jsonResponse, out result))
        {
            if (result.TokenType == TokenType.DataDictionary)
            {
                DataToken playlistToken;
                if (result.DataDictionary.TryGetValue("playlists", TokenType.DataList, out playlistToken))
                {
                    imgIdentifiersList.Clear();
                    durationList.Clear();
                    patternList.Clear();
                    playlistIndexCount = playlistToken.DataList.Count;

                    for (int i = 0; i < playlistIndexCount; i++)
                    {
                        DataToken item = playlistToken.DataList[i];

                        DataList imgIdentifiersDataList = new DataList();
                        float duration = 0.0f;
                        string pattern = "";

                        DataToken imgIdentifierToken;
                        if (item.DataDictionary.TryGetValue("imgIdentifiers", TokenType.DataList, out imgIdentifierToken))
                        {
                            for (int j = 0; j < imgIdentifierToken.DataList.Count; j++)
                            {
                                DataToken urlToken = imgIdentifierToken.DataList[j];
                                if (urlToken.TokenType == TokenType.String)
                                {
                                    imgIdentifiersDataList.Add(urlToken);
                                }
                            }
                        }
                        else if (item.DataDictionary.TryGetValue("imgIdentifiers", TokenType.String, out imgIdentifierToken))
                        {
                            imgIdentifiersDataList.Add(imgIdentifierToken);
                        }
                        DataToken durationToken;
                        if (item.DataDictionary.TryGetValue("duration", TokenType.Double, out durationToken))
                        {
                            duration = (float)durationToken.Double;
                        }
                        else
                        {
                            Debug.LogError("Record does not contain a valid 'duration' field.");
                        }
                        DataToken patternToken;
                        if (item.DataDictionary.TryGetValue("pattern", TokenType.String, out patternToken))
                        {
                            pattern = patternToken.String;
                        }
                        AddEventData(imgIdentifiersDataList, duration, pattern);
                    }
                }
                else
                {
                    Debug.LogError("JSON dictionary does not contain a 'playlist' key or it's not of type DataList.");
                }
            }
            else
            {
                Debug.LogError("Expected a JSON dictionary but received an array or other type.");
            }
            return true;
        }
        else
        {
            Debug.LogError($"Failed to Deserialize json {jsonResponse}");
            return false;
        }
    }

    public void AddEventData(DataList imgIdentifiers, float duration, string pattern)
    {
        imgIdentifiersList.Add(imgIdentifiers);
        durationList.Add(duration);
        patternList.Add(pattern);
    }

    private DataList TryGetImgIdentifiers(int index)
    {
        return imgIdentifiersList[index].DataList;
    }
    private float TryGetDuration(int index)
    {
        return (float)durationList[index];
    }
    private string TryGetPattern(int index)
    {
        return patternList[index].ToString();
    }

    private void assignRawImgTypesByIndex()
    {
        patternCounts.SetValue(new DataToken("single"), new DataToken(0));
        patternCounts.SetValue(new DataToken("left_right_split"), new DataToken(0));
        patternCounts.SetValue(new DataToken("four_split"), new DataToken(0));

        for (int i = 0; i < patternList.Count; i++) {
            string currentPattern = TryGetPattern(i);

            DataToken countToken;
            if (patternCounts.TryGetValue(currentPattern, TokenType.Int, out countToken)) {
                int count = countToken.Int + 1;
                patternCounts.SetValue(currentPattern, count);

                string assign = (count % 2 == 1) ? "Pri" : "Sec";
                rawImgAssignmentList.Add(assign);
            }
        }
    }

    private void CreateImagesDownloadList()
    {
        for (int i = 0; i < playlistIndexCount; i++)
        {
            DataToken identifierToken = TryGetImgIdentifiers(i)[0];
            string processedString = identifierToken.String
                .Replace("[", "")
                .Replace("]", "")
                .Replace("\"", "");
            individualIdentifiersByIndex = processedString.Split(',');

            playlistUrls[i] = individualIdentifiersByIndex;

            foreach (string imageIdentifier in individualIdentifiersByIndex)
            {
                string trimmedIdentifier = imageIdentifier.Trim();
                if (!imagesToDownloadList.Contains(trimmedIdentifier))
                {
                    imagesToDownloadList.Add(trimmedIdentifier);
                    if (imagesToDownloadList.Count == 100)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void ClearTextureCache()
    {
        for (int i = 0; i < textureCache.Length; i++)
        {
            textureCache[i] = null;
        }
    }

    public void DownloadNextImage()
    {
        if (downloadingImgIndex < imagesToDownloadList.Count)
        {
            string imgIdentifier = imagesToDownloadList[downloadingImgIndex].ToString();
            VRCUrl imageUrl = GetPredefinedUrl(imgIdentifier);

            imgDownloader.DownloadImage(imageUrl, null, this.GetComponent<UdonBehaviour>(), null);
            downloadingImgIndex++;
            SendCustomEventDelayedSeconds(nameof(DownloadNextImage), 5.1f);
        } else {
            hasDownloadStarted = false;
        }
    }

    private VRCUrl GetPredefinedUrl(string imgIdentifier)
    {
        string fullUrl = "https://wondernote.github.io/ds-images/" + imgIdentifier;
        foreach (VRCUrl url in predefinedUrls)
        {
            if (url.Get() == fullUrl)
            {
                return url;
            }
        }
        Debug.LogError("URL not found for identifier: " + imgIdentifier);
        return null;
    }

    public override void OnImageLoadSuccess(IVRCImageDownload result)
    {
        string downloadedUrl = result.Url.ToString();
        int lastSlashIndex = downloadedUrl.LastIndexOf('/');
        string filename = downloadedUrl.Substring(lastSlashIndex + 1);
        int cacheIndex = GetCacheIndexFromIdentifier(filename);
        Texture2D downloadedTexture = result.Result;
        textureCache[cacheIndex] = downloadedTexture;

        if (downloadedImageCount == 0)
        {
            AssignTexture(0);
        }
        downloadedImageCount++;
    }

    public override void OnImageLoadError(IVRCImageDownload result)
    {
        Debug.LogError($"画像 {result.Url} のダウンロードに失敗しました。");
    }

    private int GetCacheIndexFromIdentifier(string identifier)
    {
        int index = int.Parse(identifier.Replace("img", "").Replace(".jpg", "")) - 1;
        return index;
    }

    private void OnDestroy()
    {
        imgDownloader.Dispose();
    }

    private void AssignTexture(int index)
    {
        string displayKey = GetDisplayKeyForIndex(index);
        switch (displayKey) {
            case "singleImagePri":
                AssignTextureToDisplay(singleImagePri, playlistUrls[index]);
                break;
            case "leftRightSplitImagesPri":
                AssignTextureToLeftRightSplitDisplay(leftRightSplitImagesPri, playlistUrls[index]);
                break;
            case "fourSplitImagesPri":
                AssignTextureToFourSplitDisplay(fourSplitImagesPri, playlistUrls[index]);
                break;
            case "singleImageSec":
                AssignTextureToDisplay(singleImageSec, playlistUrls[index]);
                break;
            case "leftRightSplitImagesSec":
                AssignTextureToLeftRightSplitDisplay(leftRightSplitImagesSec, playlistUrls[index]);
                break;
            case "fourSplitImagesSec":
                AssignTextureToFourSplitDisplay(fourSplitImagesSec, playlistUrls[index]);
                break;
        }
    }

    private string GetDisplayKeyForIndex(int index)
    {
        string pattern = TryGetPattern(index);
        string assignment = rawImgAssignmentList[index].ToString();
        return GetDisplayKey(pattern, assignment);
    }

    private string GetDisplayKey(string pattern, string assignment)
    {
        switch (pattern) {
            case "single":
                return assignment == "Pri" ? "singleImagePri" : "singleImageSec";
            case "left_right_split":
                return assignment == "Pri" ? "leftRightSplitImagesPri" : "leftRightSplitImagesSec";
            case "four_split":
                return assignment == "Pri" ? "fourSplitImagesPri" : "fourSplitImagesSec";
            default:
                return "";
        }
    }

    private void AssignTextureToDisplay(RawImage display, string[] imgIdentifiers)
    {
        int cacheIndex = GetCacheIndexFromIdentifier(imgIdentifiers[0]);
        display.texture = textureCache[cacheIndex] ?? wondernoteHorizontalImage;
    }

    private void AssignTextureToLeftRightSplitDisplay(RawImage[] displays, string[] imgIdentifiers)
    {
        for (int i = 0; i < displays.Length; i++)
        {
            int cacheIndex = GetCacheIndexFromIdentifier(imgIdentifiers[i]);
            displays[i].texture = textureCache[cacheIndex] ?? wondernoteVerticalImage;
        }
    }

    private void AssignTextureToFourSplitDisplay(RawImage[] displays, string[] imgIdentifiers)
    {
        for (int i = 0; i < displays.Length; i++)
        {
            int cacheIndex = GetCacheIndexFromIdentifier(imgIdentifiers[i]);
            displays[i].texture = textureCache[cacheIndex] ?? wondernoteHorizontalImage;
        }
    }

    private void Update()
    {
        timeSinceLastApiCall += Time.deltaTime;
        if (timeSinceLastApiCall >= apiCallInterval)
        {
            InitializeImages();
            FetchPlaylist();
            timeSinceLastApiCall = 0f;
            return;
        }

        if (currentDisplayedIndex < playlistIndexCount)
        {
            timeSinceLastImageDisplay += Time.deltaTime;
            float currentDuration = TryGetDuration(currentDisplayedIndex);
            if (timeSinceLastImageDisplay >= currentDuration)
            {
                preIndex = currentDisplayedIndex;
                currentDisplayedIndex = (currentDisplayedIndex + 1) % playlistIndexCount;
                AssignTexture(currentDisplayedIndex);
                ActivateAppropriateRawImage(currentDisplayedIndex);
                timeSinceLastImageDisplay = 0f;
            }
        }

        if (isFading)
        {
            fadeTimer += Time.deltaTime;
            float alpha = Mathf.Clamp01(fadeTimer / fadeDuration);

            currentActiveGroup.alpha = 1 - alpha;
            nextActiveGroup.alpha = alpha;

            if (fadeTimer >= fadeDuration)
            {
                isFading = false;
                fadeTimer = 0.0f;
                currentActiveGroup.gameObject.SetActive(false);
                currentActiveGroup = nextActiveGroup;
            }
        }
    }

    private void ActivateAppropriateRawImage(int index)
    {
        string currentPattern = TryGetPattern(index);
        string rawImgAssignment = rawImgAssignmentList[index].ToString();

        bool isActivePri = rawImgAssignment == "Pri";
        bool isActiveSec = rawImgAssignment == "Sec";

        switch (currentPattern) {
                case "single":
                    if (isActivePri) nextActiveGroup = singleImagePriGroup;
                    if (isActiveSec) nextActiveGroup = singleImageSecGroup;
                    break;
                case "left_right_split":
                    if (isActivePri) nextActiveGroup = leftRightSplitImagesPriGroup;
                    if (isActiveSec) nextActiveGroup = leftRightSplitImagesSecGroup;
                    break;
                case "four_split":
                    if (isActivePri) nextActiveGroup = fourSplitImagesPriGroup;
                    if (isActiveSec) nextActiveGroup = fourSplitImagesSecGroup;
                    break;
            }

        if (index == 0) {
            nextActiveGroup.alpha = 1.0f;
            nextActiveGroup.gameObject.SetActive(true);
            currentActiveGroup = nextActiveGroup;
        } else {
            nextActiveGroup.gameObject.SetActive(true);
            isFading = true;
        }
    }
}
