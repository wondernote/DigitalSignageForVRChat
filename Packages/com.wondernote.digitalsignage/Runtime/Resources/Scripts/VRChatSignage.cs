
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

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VRChatSignage : UdonSharpBehaviour
{
    [Header("API Settings")]
    [SerializeField] private VRCUrl activeApiUrl;
    [SerializeField] private VRCUrl[] pcImagesUrls;
    [SerializeField] private VRCUrl[] androidImagesUrls;
    private VRCUrl[] activeChunkImagesUrls;

    private float apiCallInterval = 6 * 60 * 60;

    private int currentDisplayedIndex;
    private float timeSinceLastApiCall = 0f;
    private float timeSinceLastImageDisplay;

    [Header("Display Settings")]
    [SerializeField] private Texture2D defaultHorizontalImage;
    [SerializeField] private Texture2D defaultVerticalImage;
    [SerializeField] private Texture2D fetchErrorImage;

    [SerializeField] private RawImage singleImagePri;
    [SerializeField] private RawImage[] leftRightSplitImagesPri;
    [SerializeField] private RawImage[] fourSplitImagesPri;
    [SerializeField] private RawImage singleImageSec;
    [SerializeField] private RawImage[] leftRightSplitImagesSec;
    [SerializeField] private RawImage[] fourSplitImagesSec;

    [SerializeField] private CanvasGroup singleImagePriGroup;
    [SerializeField] private CanvasGroup leftRightSplitImagesPriGroup;
    [SerializeField] private CanvasGroup fourSplitImagesPriGroup;
    [SerializeField] private CanvasGroup singleImageSecGroup;
    [SerializeField] private CanvasGroup leftRightSplitImagesSecGroup;
    [SerializeField] private CanvasGroup fourSplitImagesSecGroup;

    private CanvasGroup currentActiveGroup;
    private CanvasGroup nextActiveGroup;
    private float fadeDuration = 1.0f;
    private float fadeTimer;
    private bool isFading;

    DataList imgIdentifiersList = new DataList();
    DataList durationList = new DataList();
    DataList patternList = new DataList();

    private string[][] playlistUrls;

    DataList rawImgAssignmentList = new DataList();
    private DataDictionary patternCounts = new DataDictionary();

    private int playlistIndexCount;

    private TextureFormat textureFormat;
    private bool isAndroid;
    private bool isFirstImagesDownloaded = false;
    private const int FRAME_PROCESS_LIMIT_MS = 3;

    private System.Diagnostics.Stopwatch splitImgsProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch imgsJsonChunksParseProcessTime = new System.Diagnostics.Stopwatch();
    private System.Diagnostics.Stopwatch imgsJsonParseProcessTime = new System.Diagnostics.Stopwatch();

    private int chunkCount;
    private int loadingChunkIndex = 0;
    private string arrayContent_imgs;
    private int splitStartIndex_imgs;
    private int imgsJsonChunkIndex;
    private int imgsJsonParseIndex;
    private int groupIndex;

    private DataList signageImagesList = new DataList();
    private DataList globalGroups = new DataList();
    private DataList imgsJsonChunksList = new DataList();
    private DataList parsedChunksImgsList = new DataList();
    private DataToken cachedJsonResultImgs;

    private void Start()
    {
        #if UNITY_ANDROID
            activeChunkImagesUrls = androidImagesUrls;
            textureFormat = TextureFormat.ETC_RGB4Crunched;
            isAndroid = true;
        #else
            activeChunkImagesUrls = pcImagesUrls;
            textureFormat = TextureFormat.DXT1Crunched;
            isAndroid = false;
        #endif

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
        VRCStringDownloader.LoadUrl(activeApiUrl, this.GetComponent<UdonBehaviour>());
    }

    public override void OnStringLoadSuccess(IVRCStringDownload download)
    {
        if (download.Url == activeApiUrl)
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
            signageImagesList.Clear();

            DownloadNextImage();
            ActivateAppropriateRawImage(0);
        }
        else
        {
            StartImgsJsonParsing(download.Result);
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (result.Url == activeApiUrl)
        {
            ShowErrorImage();
            Debug.LogError($"プレイリストの取得に失敗しました。 from {result.Url}: {result.ErrorCode} - {result.Error}");
        }
        else
        {
            Debug.LogError($"Error loading signage_images string from {result.Url}: {result.ErrorCode} - {result.Error}");
        }
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

                int pcImagesChunkCountValue = 0;
                DataToken pcImagesChunkCountToken;
                if (result.DataDictionary.TryGetValue("pcImagesChunkCount", TokenType.Double, out pcImagesChunkCountToken))
                {
                    pcImagesChunkCountValue = (int)pcImagesChunkCountToken.Double;
                }
                else
                {
                    Debug.LogError("pcImagesChunkCountが見つからないか、数値型ではありません。");
                }

                int androidImagesChunkCountValue = 0;
                DataToken androidImagesChunkCountToken;
                if (result.DataDictionary.TryGetValue("androidImagesChunkCount", TokenType.Double, out androidImagesChunkCountToken))
                {
                    androidImagesChunkCountValue = (int)androidImagesChunkCountToken.Double;
                }
                else
                {
                    Debug.LogError("androidImagesChunkCountが見つからないか、数値型ではありません。");
                }

                if (isAndroid) {
                    chunkCount = androidImagesChunkCountValue;
                } else {
                    chunkCount = pcImagesChunkCountValue;
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

    private void AddEventData(DataList imgIdentifiers, float duration, string pattern)
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
        string[] individualIdentifiersByIndex;

        for (int i = 0; i < playlistIndexCount; i++)
        {
            DataToken identifierToken = TryGetImgIdentifiers(i)[0];
            string processedString = identifierToken.String
                .Replace("[", "")
                .Replace("]", "")
                .Replace("\"", "");

            individualIdentifiersByIndex = processedString.Split(',');

            playlistUrls[i] = individualIdentifiersByIndex;
        }
    }

    private void DownloadNextImage()
    {
        if (loadingChunkIndex < chunkCount)
        {
            VRCUrl chunkImagesUrl = activeChunkImagesUrls[loadingChunkIndex];
            VRCStringDownloader.LoadUrl(chunkImagesUrl, this.GetComponent<UdonBehaviour>());
            return;
        }

        splitImgsProcessTime = null;
        imgsJsonChunksParseProcessTime = null;
        imgsJsonParseProcessTime = null;
        activeChunkImagesUrls = null;
        globalGroups.Clear();
    }

    private void StartImgsJsonParsing(string jsonResponse)
    {
        arrayContent_imgs = "";
        splitStartIndex_imgs = 0;
        imgsJsonChunkIndex = 0;
        imgsJsonParseIndex = 0;

        int start = jsonResponse.IndexOf('[');
        int end = jsonResponse.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            arrayContent_imgs = jsonResponse.Substring(start + 1, end - start - 1);
            SplitImgsJsonChunksAsync();
        }
        else
        {
            Debug.LogError("Failed to find valid Images JSON array in response.");
        }
    }

    public void SplitImgsJsonChunksAsync()
    {
        splitImgsProcessTime.Restart();

        while (splitStartIndex_imgs < arrayContent_imgs.Length)
        {
            int nextComma = arrayContent_imgs.IndexOf("},", splitStartIndex_imgs);
            bool isLastElement = nextComma == -1;

            int endIndex = isLastElement ? arrayContent_imgs.Length : nextComma + 1;
            string element = arrayContent_imgs.Substring(splitStartIndex_imgs, endIndex - splitStartIndex_imgs).Trim();

            if (!element.StartsWith("{")) element = $"{{{element}";
            if (!element.EndsWith("}")) element = $"{element}}}";

            imgsJsonChunksList.Add(new DataToken(element));
            splitStartIndex_imgs = endIndex + 1;

            if (splitImgsProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(SplitImgsJsonChunksAsync), 1);
                return;
            }
        }

        ParseImgsJsonChunksAsync();
    }

    public void ParseImgsJsonChunksAsync()
    {
        imgsJsonChunksParseProcessTime.Restart();

        while (imgsJsonChunkIndex < imgsJsonChunksList.Count)
        {
            string chunk = imgsJsonChunksList[imgsJsonChunkIndex].String;

            if (VRCJson.TryDeserializeFromJson(chunk, out DataToken result))
            {
                parsedChunksImgsList.Add(result);
            }
            else
            {
                Debug.LogError($"Failed to parse Images chunk {imgsJsonChunkIndex}/{imgsJsonChunksList.Count}.");
            }

            imgsJsonChunkIndex++;

            if (imgsJsonChunksParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
            {
                SendCustomEventDelayedFrames(nameof(ParseImgsJsonChunksAsync), 1);
                return;
            }
        }

        cachedJsonResultImgs = CombineChunksToCachedResult(parsedChunksImgsList);
        ParseSignageImagesJson();
    }

    private DataToken CombineChunksToCachedResult(DataList chunksList)
    {
        var dataList = new DataList();
        for (int i = 0; i < chunksList.Count; i++)
        {
            DataToken chunk = chunksList[i];
            if (chunk.TokenType == TokenType.DataDictionary)
            {
                dataList.Add(chunk);
            }
            else
            {
                Debug.LogError($"Parsed chunk {i} is not a DataDictionary. Skipping...");
            }
        }
        return new DataToken(dataList);
    }

    public void ParseSignageImagesJson()
    {
        imgsJsonParseProcessTime.Restart();

        if (cachedJsonResultImgs.TokenType == TokenType.DataList)
        {
            while (imgsJsonParseIndex < cachedJsonResultImgs.DataList.Count)
            {
                groupIndex = 0;
                DataToken partToken = cachedJsonResultImgs.DataList[imgsJsonParseIndex];
                imgsJsonParseIndex++;

                if (partToken.TokenType == TokenType.DataDictionary)
                {
                    DataDictionary partDict = partToken.DataDictionary;
                    string imageId = partDict["image_id"].String;

                    string prefetch = partDict["base64SignageImage"].String;
                    partDict["prefetchedB64"] = new DataToken(prefetch);
                    partDict.Remove("base64SignageImage");

                    bool foundGroup = false;
                    while (groupIndex < globalGroups.Count)
                    {
                        DataDictionary groupInfo = globalGroups[groupIndex].DataDictionary;
                        string currentKey = groupInfo["image_id"].String;
                        if (currentKey == imageId)
                        {
                            if (groupInfo["finalized"].String == "false")
                            {
                                groupInfo["parts"].DataList.Add(partToken);
                            }
                            foundGroup = true;
                            break;
                        }
                        groupIndex++;
                    }

                    if (!foundGroup)
                    {
                        DataDictionary newGroupInfo = new DataDictionary();
                        DataList partList = new DataList();
                        partList.Add(partToken);

                        newGroupInfo.Add("image_id", new DataToken(imageId));
                        newGroupInfo.Add("parts", new DataToken(partList));
                        newGroupInfo.Add("finalized", new DataToken("false"));
                        globalGroups.Add(new DataToken(newGroupInfo));
                    }
                }
                else
                {
                    Debug.LogError("Element is not a DataDictionary.");
                }

                if (imgsJsonParseProcessTime.ElapsedMilliseconds > FRAME_PROCESS_LIMIT_MS)
                {
                    SendCustomEventDelayedFrames(nameof(ParseSignageImagesJson), 1);
                    return;
                }
            }
        }
        MergeAndFinalizeImages();

        cachedJsonResultImgs = default;
        loadingChunkIndex++;

        if (loadingChunkIndex == 1)
        {
            isFirstImagesDownloaded = true;
            AssignTexture(0);
        }

        imgsJsonChunksList.Clear();
        parsedChunksImgsList.Clear();
        DownloadNextImage();
    }

    private void MergeAndFinalizeImages()
    {
        int groupIndex = 0;

        while (groupIndex < globalGroups.Count)
        {
            DataToken groupToken = globalGroups[groupIndex];
            if (groupToken.TokenType == TokenType.DataDictionary)
            {
                DataDictionary groupInfo = groupToken.DataDictionary;

                if (groupInfo["finalized"].String == "true")
                {
                    groupIndex++;
                    continue;
                }

                DataList partsList = groupInfo["parts"].DataList;
                string imageId = groupInfo["image_id"].String;

                int maxSerial = 10;

                if (partsList.Count == maxSerial)
                {
                    string combinedBase64 = "";
                    int width = 0;
                    int height = 0;

                    int indexPart = 0;
                    while (indexPart < partsList.Count)
                    {
                        DataDictionary dictP = partsList[indexPart].DataDictionary;
                        combinedBase64 += dictP["prefetchedB64"].String;
                        if (indexPart == 0)
                        {
                            width = (int)dictP["width"].Double;
                            height = (int)dictP["height"].Double;
                        }
                        indexPart++;
                    }

                    DataDictionary newImageDict = new DataDictionary();
                    newImageDict.Add("image_id", new DataToken(imageId));
                    newImageDict.Add("width", new DataToken(width));
                    newImageDict.Add("height", new DataToken(height));
                    newImageDict.Add("base64SignageImage", new DataToken(combinedBase64));

                    signageImagesList.Add(new DataToken(newImageDict));

                    groupInfo["finalized"] = new DataToken("true");
                }
            }

            groupIndex++;
        }
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
        SetSignageImage(imgIdentifiers[0], display);
    }

    private void AssignTextureToLeftRightSplitDisplay(RawImage[] displays, string[] imgIdentifiers)
    {
        for (int i = 0; i < displays.Length; i++)
        {
            SetSignageImage(imgIdentifiers[i], displays[i]);
        }
    }

    private void AssignTextureToFourSplitDisplay(RawImage[] displays, string[] imgIdentifiers)
    {
        for (int i = 0; i < displays.Length; i++)
        {
            SetSignageImage(imgIdentifiers[i], displays[i]);
        }
    }

    private void SetSignageImage(string identifier, RawImage rawImage)
    {
        for (int i = 0; i < signageImagesList.Count; i++)
        {
            var signageImgsDataDictionary = signageImagesList[i];
            if (signageImgsDataDictionary.TokenType == TokenType.DataDictionary)
            {
                var signageImgsDict = signageImgsDataDictionary.DataDictionary;
                if (signageImgsDict["image_id"].String == identifier)
                {
                    string base64SignageImage = signageImgsDict["base64SignageImage"].String;
                    int signageImageWidth = (int)signageImgsDict["width"].Double;
                    int signageImageHeight = (int)signageImgsDict["height"].Double;

                    byte[] imageBytes = Convert.FromBase64String(base64SignageImage);
                    Texture2D newTexture = new Texture2D(signageImageWidth, signageImageHeight, textureFormat, true, false);
                    newTexture.LoadRawTextureData(imageBytes);
                    newTexture.Apply();

                    rawImage.texture = newTexture;

                    rawImage.uvRect = new Rect(0, 0, 1, 1);
                    rawImage.uvRect = new Rect(0, 1, 1, -1);
                    return;
                }
            }
            else
            {
                Debug.LogError("An element in signageImgsDataDictionary is not a DataDictionary.");
            }
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

        if ((currentDisplayedIndex < playlistIndexCount) && isFirstImagesDownloaded)
        {
            timeSinceLastImageDisplay += Time.deltaTime;
            float currentDuration = TryGetDuration(currentDisplayedIndex);
            if (timeSinceLastImageDisplay >= currentDuration)
            {
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
