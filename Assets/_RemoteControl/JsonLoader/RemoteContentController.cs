using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
public partial class RemoteContentController : MonoBehaviour
{
    // =========================================================
    // STATE
    // =========================================================

    public enum RemoteContentState
    {
        Idle,
        Checking,
        Downloading,
        Ready,
        OfflineReady,
        Failed
    }


    // =========================================================
    // UNITY IMAGE BINDING
    // =========================================================

    [Serializable]
    public class ImageElementBinding
    {
        [Tooltip("Must match the image key in JSON. Example: bg, claim, close")]
        public string key;

        [Tooltip("UI Image that receives the downloaded sprite.")]
        public Image targetImage;

        [Tooltip("Optional local fallback sprite.")]
        public Sprite fallbackSprite;


        [NonSerialized]
        public Sprite runtimeSprite;

        [NonSerialized]
        public Texture2D runtimeTexture;

        [NonSerialized]
        public string loadedPath;

        [NonSerialized]
        public Sprite originalSprite;

        [NonSerialized]
        public bool originalSpriteCaptured;
    }


    // =========================================================
    // UNITY SLIDE / PANEL BINDING
    // =========================================================

    [Serializable]
    public class RemoteSlideBinding
    {
        [Tooltip("Must match the ID in the JSON.")]
        public string sliderId;

        [Tooltip("Optional. JSON enabled controls this GameObject.")]
        public GameObject sliderParent;

        [Tooltip("Used if this slider ID doesn't exist in JSON.")]
        public bool defaultEnabled = true;

        [Header("Images Inside This Panel")]
        public List<ImageElementBinding> imageElements =
            new List<ImageElementBinding>();
    }


    // =========================================================
    // JSON DATA
    // =========================================================

    [Serializable]
    public partial class RemoteConfigData
    {
        public bool enabled = true;

        public RemoteSlideData[] sliders;

        public RemoteStaticPanelData[] staticPanels;
    }


    [Serializable]
    public class RemoteSlideData
    {
        public string id;

        public bool enabled = true;

        // 0 = first option
        // 1 = second option
        // 2 = third option...
        public int selectedOption;

        public RemoteOptionData[] options;
    }


    [Serializable]
    public class RemoteOptionData
    {
        public RemoteImageData[] images;
    }


    [Serializable]
    public class RemoteImageData
    {
        // Example:
        // bg
        // claim
        // close
        public string key;

        public int version = 1;

        public string url;

        // 0 = don't validate exact size
        public long size;

        // Empty = don't validate SHA256
        public string sha256;
    }


    // =========================================================
    // DOWNLOAD JOB
    // =========================================================

    public class DownloadJob
    {
        public string sliderId;

        public int optionIndex;

        public RemoteImageData image;

        public string finalPath;

        public string tempPath;

        public bool success;
    }


    // =========================================================
    // GENERAL - INSPECTOR
    // =========================================================

    [Header("GENERAL")]

    public string configUrl =
        "http://127.0.0.1:8000/remote_config_example.json";


    [Tooltip("Optional. Root JSON enabled controls this parent.")]
    public GameObject mainParent;


    public bool downloadOnStart = true;


    [Header("REMOTE CONFIG OFF")]

    [Tooltip(
        "When root JSON enabled is false, keep the last downloaded content. " +
        "If no valid downloaded config exists, restore the original/fallback Unity UI. " +
        "When false, skip remote processing and invoke the existing static-sequence finish flow."
    )]
    public bool useLocalContentWhenRemoteConfigDisabled = true;


    [Space(10)]

    public List<RemoteSlideBinding> slides =
        new List<RemoteSlideBinding>();


    // =========================================================
    // IMAGE SETTINGS - INSPECTOR
    // =========================================================

    [Header("IMAGE SETTINGS")]

    public float pixelsPerUnit = 100f;


    public bool validateFileSize = true;


    public bool validateSha256 = true;


    // =========================================================
    // DOWNLOAD SETTINGS - INSPECTOR
    // =========================================================

    [Header("DOWNLOAD SETTINGS")]

    [Min(1)]
    public int timeoutSeconds = 5;


    [Tooltip("Retries AFTER the first attempt.")]
    [Min(0)]
    public int maxRetries = 1;


    [Min(0)]
    public float retryDelaySeconds = 1f;


    public bool exponentialBackoff = true;


    // =========================================================
    // VISUAL COMMIT - INSPECTOR
    // =========================================================

    [Header("VISUAL COMMIT")]

    [Tooltip("Downloads stop at this percentage. The remaining progress is reserved for applying and verifying the UI.")]
    [Range(0.50f, 0.95f)]
    public float downloadPhaseEnd = 0.90f;

    [Tooltip("Progress shown immediately after downloaded content is applied to Unity UI targets.")]
    [Range(0.90f, 0.98f)]
    public float applyPhaseEnd = 0.96f;

    [Tooltip("Highest progress shown while verifying visuals. 100% is only shown after every target is committed and stable.")]
    [Range(0.95f, 0.999f)]
    public float verifyPhaseEnd = 0.99f;

    [Tooltip("How long dynamic/static visuals must remain correct before the loading panel is allowed to disappear.")]
    [Min(0.02f)]
    public float visualCommitStableSeconds = 0.12f;

    [Tooltip("Maximum time to re-apply/verify visuals before falling back to the previous working content.")]
    [Min(0.10f)]
    public float visualCommitMaxWaitSeconds = 1.50f;


    // =========================================================
    // DOWNLOAD UI - INSPECTOR
    // =========================================================

    [Header("DOWNLOAD UI")]

    [Tooltip("Whole loading / downloading panel.")]
    public GameObject loadingParent;


    [Tooltip("Progress slider.")]
    public Slider progressSlider;


    [Tooltip("TMP progress text.")]
    public TextMeshProUGUI progressText;


    [Tooltip("TMP status text.")]
    public TextMeshProUGUI statusText;


    [Tooltip("Retry button.")]
    public Button retryButton;


    // =========================================================
    // FAILURE BEHAVIOR - INSPECTOR
    // =========================================================

    [Header("FAILURE BEHAVIOR")]

    [Tooltip(
        "If true, Retry button appears after failure. " +
        "If false, loading UI simply disappears and fallback content is used."
    )]
    public bool showRetryButtonOnFailure = false;


    [Tooltip(
        "Hide loading panel completely after failure."
    )]
    public bool hideLoadingPanelOnFailure = true;


    // =========================================================
    // PUBLIC RUNTIME INFO
    // =========================================================

    [Header("RUNTIME DEBUG")]

    public RemoteContentState state =
        RemoteContentState.Idle;


    [Range(0f, 1f)]
    public float downloadProgress;


    public int totalFilesToDownload;


    public int downloadedFiles;


    public int failedFiles;


    public bool isBusy;


    // =========================================================
    // INTERNAL
    // =========================================================

    private RemoteConfigData activeConfig;


    // =========================================================
    // PATHS
    // =========================================================

    public string CacheRoot
    {
        get
        {
            return Path.Combine(
                Application.persistentDataPath,
                "RemoteContentCache"
            );
        }
    }


    public string ActiveConfigPath
    {
        get
        {
            return Path.Combine(
                CacheRoot,
                "active_config.json"
            );
        }
    }


    public string PendingConfigPath
    {
        get
        {
            return Path.Combine(
                CacheRoot,
                "pending_config.json"
            );
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        // This controller intentionally runs late (DefaultExecutionOrder 10000).
        // That lets normal UI Awake/OnEnable initialization happen first, then
        // cached remote visuals are applied before the first rendered frame.
        AutoRegisterStaticPanels();
        InitializeStaticBindings();
        CaptureOriginalSlideSprites();
        ValidateScenePilotConnection(false);

        Directory.CreateDirectory(
            CacheRoot
        );

        DeleteStalePartFiles();

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(
                RetryFailedDownloads
            );

            retryButton.gameObject.SetActive(
                false
            );
        }

        // Keep the cover available while cached/remote visuals settle.
        if (downloadOnStart && loadingParent != null)
        {
            loadingParent.SetActive(
                true
            );
        }

        // Read/apply the last known-good config in Awake, not only Start.
        // This is intentionally repeated in Start to win over other Start()
        // methods that may reset Image.sprite to their serialized defaults.
        LoadActiveConfig();
    }


    private void Start()
    {
        // Re-assert cached content after ordinary Start() methods have run.
        // TryLoadElementImage() will not re-decode the texture when the same
        // cached path is already loaded.
        if (activeConfig != null)
        {
            ApplyConfig(
                activeConfig
            );
        }

        if (downloadOnStart)
        {
            StartCoroutine(
                SyncRemoteContent()
            );
        }
        else if (loadingParent != null)
        {
            loadingParent.SetActive(
                false
            );
        }
    }


    private void OnDisable()
    {
        // Unity stops coroutines when their GameObject is deactivated. Never
        // leave retries permanently blocked if an owning UI root is hidden.
        isBusy = false;
    }


    private void OnDestroy()
    {
        ReleaseStaticImages(true);

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(
                RetryFailedDownloads
            );
        }


        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            DestroySlideRuntimeImages(
                binding
            );
        }
    }


    // =========================================================
    // PUBLIC RETRY
    // =========================================================

    public void RetryFailedDownloads()
    {
        if (isBusy)
            return;


        if (loadingParent != null)
        {
            loadingParent.SetActive(
                true
            );
        }


        SetDownloadUIState(
            true,
            false
        );


        StartCoroutine(
            SyncRemoteContent()
        );
    }


    public void RefreshNow()
    {
        RetryFailedDownloads();
    }


    // =========================================================
    // LOAD PREVIOUS WORKING CONFIG
    // =========================================================

    public void LoadActiveConfig()
    {
        activeConfig =
            LoadConfigFromFile(
                ActiveConfigPath
            );

        if (activeConfig != null)
        {
            ApplyConfig(
                activeConfig
            );

            state =
                RemoteContentState.OfflineReady;

            SetStatus(
                "Loaded saved content."
            );
        }
        else
        {
            ApplyInspectorFallbacks();

            state =
                RemoteContentState.Idle;

            SetStatus(
                "No saved remote content."
            );
        }

        staticStartupComplete = true;
    }


    // =========================================================
    // MAIN SYNC
    // =========================================================

    public IEnumerator SyncRemoteContent()
    {
        if (isBusy)
            yield break;


        isBusy = true;


        ResetProgress();


        state =
            RemoteContentState.Checking;


        if (loadingParent != null)
        {
            loadingParent.SetActive(
                true
            );
        }


        // Checking / downloading:
        // progress = ON
        // retry = OFF
        SetDownloadUIState(
            true,
            false
        );


        SetStatus(
            "Checking remote content..."
        );


        // Keep the network phase below 100%. The last part of the progress
        // bar is reserved for applying sprites and proving that Unity's UI
        // actually holds the new visuals before the loading cover disappears.
        float downloadEnd =
            Mathf.Clamp(
                downloadPhaseEnd,
                0.50f,
                0.95f
            );

        float applyEnd =
            Mathf.Clamp(
                applyPhaseEnd,
                downloadEnd,
                0.98f
            );

        float verifyEnd =
            Mathf.Clamp(
                verifyPhaseEnd,
                applyEnd,
                0.999f
            );


        // =====================================================
        // DOWNLOAD JSON
        // =====================================================

        string json = null;

        string configError = null;


        yield return StartCoroutine(
            DownloadConfig(
                (success, result, error) =>
                {
                    if (success)
                    {
                        json = result;
                    }
                    else
                    {
                        configError = error;
                    }
                }
            )
        );


        if (
            string.IsNullOrWhiteSpace(
                json
            )
        )
        {
            FinishWithFailure(
                "Could not download config: " +
                (
                    string.IsNullOrEmpty(
                        configError
                    )
                        ? "Unknown error"
                        : configError
                )
            );


            isBusy = false;

            yield break;
        }


        // =====================================================
        // PARSE
        // =====================================================

        RemoteConfigData pendingConfig =
            ParseConfig(
                json
            );


        if (
            !ValidateConfig(
                pendingConfig,
                out string configValidationError
            )
        )
        {
            FinishWithFailure(
                "Invalid remote config:\n" +
                configValidationError
            );


            isBusy = false;

            yield break;
        }


        // A valid global OFF response is a control signal, not a new active
        // content version. Handle it before writing/promoting pending JSON so
        // the last known-good downloaded config and files remain untouched.
        if (!pendingConfig.enabled)
        {
            FinishRemoteConfigDisabled();
            yield break;
        }


        // =====================================================
        // SAVE PENDING JSON
        // =====================================================

        if (
            !SafeWriteText(
                PendingConfigPath,
                json
            )
        )
        {
            FinishWithFailure(
                "Could not save pending config."
            );


            isBusy = false;

            yield break;
        }


        // =====================================================
        // BUILD DOWNLOAD QUEUE
        // =====================================================

        List<DownloadJob> queue =
            new List<DownloadJob>();


        if (
            !BuildDownloadQueue(
                pendingConfig,
                queue,
                out string queueError
            )
        )
        {
            FinishWithFailure(
                queueError
            );


            isBusy = false;

            yield break;
        }


        totalFilesToDownload =
            queue.Count;


        downloadedFiles = 0;

        failedFiles = 0;


        downloadProgress =
            queue.Count == 0
                ? downloadEnd
                : 0f;


        UpdateProgressUI();


        Debug.Log(
            "[RemoteContent] Files requiring download: " +
            queue.Count
        );


        // =====================================================
        // DOWNLOAD FILES
        // =====================================================

        bool anyFailed = false;


        if (queue.Count > 0)
        {
            state =
                RemoteContentState.Downloading;


            for (
                int i = 0;
                i < queue.Count;
                i++
            )
            {
                DownloadJob job =
                    queue[i];


                yield return StartCoroutine(
                    DownloadJobWithRetries(
                        job,
                        i,
                        queue.Count
                    )
                );


                if (job.success)
                {
                    downloadedFiles++;
                }
                else
                {
                    failedFiles++;

                    anyFailed = true;
                }


                downloadProgress =
                    downloadEnd *
                    ((float)(i + 1) /
                    queue.Count);


                UpdateProgressUI();
            }
        }


        // =====================================================
        // DOWNLOAD FAILURE
        // =====================================================

        if (anyFailed)
        {
            FinishWithFailure(
                failedFiles +
                " file(s) failed to download."
            );


            isBusy = false;

            yield break;
        }


        // =====================================================
        // FINAL FILE CHECK
        // =====================================================

        if (
            !AllRequiredFilesAvailable(
                pendingConfig,
                out string fileError
            )
        )
        {
            FinishWithFailure(
                "Content validation failed:\n" +
                fileError
            );


            isBusy = false;

            yield break;
        }


        // =====================================================
        // APPLY PENDING CONTENT - DO NOT PROMOTE YET
        // =====================================================
        // The previous activeConfig stays untouched until the new UI has
        // actually committed. If anything below fails, FinishWithFailure()
        // can safely restore the previous working content.

        SetStatus(
            "Applying downloaded content..."
        );

        downloadProgress =
            Mathf.Max(
                downloadProgress,
                applyEnd
            );

        UpdateProgressUI();


        ApplyConfig(
            pendingConfig
        );


        // Force both dynamic slides and static panels to their exact pending
        // values while the loading panel is still covering the game.
        string dynamicCommitError = null;
        string staticCommitError = null;

        bool dynamicApplied =
            ApplyDynamicSlidesImmediately(
                pendingConfig,
                out dynamicCommitError
            );

        bool staticApplied =
            ApplyStaticPanelsImmediately(
                pendingConfig,
                out staticCommitError
            );

        if (!dynamicApplied && !string.IsNullOrEmpty(dynamicCommitError))
        {
            Debug.LogWarning(
                "[RemoteContent] Initial dynamic visual commit is not ready: " +
                dynamicCommitError,
                this
            );
        }

        if (!staticApplied && !string.IsNullOrEmpty(staticCommitError))
        {
            Debug.LogWarning(
                "[RemoteContent] Initial static visual commit is not ready: " +
                staticCommitError,
                this
            );
        }


        // =====================================================
        // COMBINED VISUAL COMMIT BARRIER
        // =====================================================
        // A file being downloaded is NOT considered finished from the
        // player's point of view. The loading panel remains visible until:
        //   1) selected dynamic sprites are assigned,
        //   2) static sprites/colors are assigned,
        //   3) they survive Canvas/Animator/other-script updates for a short
        //      continuous stability window.

        SetStatus(
            "Preparing UI..."
        );

        float requiredStableSeconds =
            Mathf.Max(
                0.02f,
                visualCommitStableSeconds
            );

        float maxCommitWaitSeconds =
            Mathf.Max(
                requiredStableSeconds,
                visualCommitMaxWaitSeconds
            );

        float stableSince = -1f;
        float commitStart = Time.realtimeSinceStartup;
        float commitDeadline = commitStart + maxCommitWaitSeconds;

        while (Time.realtimeSinceStartup < commitDeadline)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;

            bool dynamicReady =
                VerifyDynamicSlidesApplied(
                    pendingConfig,
                    out dynamicCommitError
                );

            bool staticReady =
                VerifyStaticPanelsApplied(
                    pendingConfig,
                    out staticCommitError
                );

            float elapsed01 =
                Mathf.Clamp01(
                    (Time.realtimeSinceStartup - commitStart) /
                    maxCommitWaitSeconds
                );

            downloadProgress =
                Mathf.Lerp(
                    applyEnd,
                    verifyEnd,
                    elapsed01
                );

            UpdateProgressUI();

            if (dynamicReady && staticReady)
            {
                if (stableSince < 0f)
                {
                    stableSince =
                        Time.realtimeSinceStartup;
                }

                if (
                    Time.realtimeSinceStartup -
                    stableSince >=
                    requiredStableSeconds
                )
                {
                    break;
                }
            }
            else
            {
                stableSince = -1f;

                // Re-apply only while the downloader is still covering the
                // game. This catches Animators or scripts that overwrite a
                // target during startup.
                if (!dynamicReady)
                {
                    ApplyDynamicSlidesImmediately(
                        pendingConfig,
                        out dynamicCommitError
                    );
                }

                if (!staticReady)
                {
                    ApplyStaticPanelsImmediately(
                        pendingConfig,
                        out staticCommitError
                    );
                }
            }
        }


        bool visualsStable =
            stableSince >= 0f &&
            Time.realtimeSinceStartup -
            stableSince >=
            requiredStableSeconds;

        if (!visualsStable)
        {
            string commitError =
                !string.IsNullOrEmpty(dynamicCommitError)
                    ? dynamicCommitError
                    : staticCommitError;

            Debug.LogError(
                "[RemoteContent] New content downloaded but UI did not settle. " +
                (string.IsNullOrEmpty(commitError)
                    ? "Unknown UI ownership/Animator conflict."
                    : commitError),
                this
            );

            FinishWithFailure(
                "Content downloaded, but the new UI could not be applied safely."
            );

            isBusy = false;
            yield break;
        }


        // One final end-of-frame pass while the loading panel still covers
        // everything. 100% is deliberately not shown before this point.
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        if (
            !VerifyDynamicSlidesApplied(
                pendingConfig,
                out dynamicCommitError
            ) ||
            !VerifyStaticPanelsApplied(
                pendingConfig,
                out staticCommitError
            )
        )
        {
            string finalError =
                !string.IsNullOrEmpty(dynamicCommitError)
                    ? dynamicCommitError
                    : staticCommitError;

            Debug.LogError(
                "[RemoteContent] UI changed during final reveal frame: " +
                finalError,
                this
            );

            FinishWithFailure(
                "Content downloaded, but the UI changed before reveal."
            );

            isBusy = false;
            yield break;
        }


        downloadProgress = verifyEnd;
        UpdateProgressUI();


        // =====================================================
        // PROMOTE ONLY AFTER VISUAL COMMIT SUCCEEDS
        // =====================================================

        if (
            !PromotePendingConfig(
                json
            )
        )
        {
            FinishWithFailure(
                "Could not activate new config."
            );


            isBusy = false;

            yield break;
        }


        activeConfig =
            pendingConfig;


        // =====================================================
        // SUCCESS / REVEAL
        // =====================================================

        downloadProgress = 1f;


        UpdateProgressUI();


        SetStatus(
            "Remote content ready."
        );


        state =
            RemoteContentState.Ready;


        SetDownloadUIState(
            false,
            false
        );


        // Clear this before hiding UI. If the controller was accidentally
        // placed under loadingParent, SetActive(false) stops this coroutine.
        isBusy = false;


        if (loadingParent != null)
        {
            loadingParent.SetActive(
                false
            );
        }


        FinishSequenceWhenDownloadLoadingIsOnlyEnabledPanel(
            activeConfig
        );
    }


    private void FinishSequenceWhenDownloadLoadingIsOnlyEnabledPanel(
        RemoteConfigData config)
    {
        if (config == null || config.staticPanels == null)
            return;

        bool hasEnabledDownloadLoading = false;

        foreach (RemoteStaticPanelData panel in config.staticPanels)
        {
            if (panel == null || !panel.enabled)
                continue;

            if (IsStaticSequencePanelId(panel.id))
                return;

            if (
                string.Equals(
                    panel.id,
                    "download_loading",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                (
                    !string.IsNullOrEmpty(panel.id) &&
                    panel.id.StartsWith(
                        "download_loading_",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                hasEnabledDownloadLoading = true;
            }
        }

        if (hasEnabledDownloadLoading)
        {
            FinishStaticPanelSequence(
                null,
                "Download completed and download_loading is the only enabled panel; finishing the static panel sequence."
            );
        }
    }


    // =========================================================
    // DOWNLOAD CONFIG
    // =========================================================

    public IEnumerator DownloadConfig(
        Action<bool, string, string> callback)
    {
        int attempts =
            Mathf.Max(
                1,
                maxRetries + 1
            );


        string lastError =
            "";


        for (
            int attempt = 1;
            attempt <= attempts;
            attempt++
        )
        {
            SetStatus(
                attempt == 1
                    ? "Checking config..."
                    :
                    "Checking config... Retry " +
                    (attempt - 1) +
                    "/" +
                    maxRetries
            );


            // The live config is mutable at one permanent URL. Give every
            // check a unique query and explicitly bypass intermediary caches;
            // otherwise a CDN/proxy can return the previous published JSON
            // several times even though the server file has changed.
            string freshConfigUrl =
                configUrl +
                (configUrl.Contains("?") ? "&" : "?") +
                "_scenepilot=" +
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                "_" +
                attempt;

            using (
                UnityWebRequest request =
                    UnityWebRequest.Get(
                        freshConfigUrl
                    )
            )
            {
                request.timeout =
                    timeoutSeconds;

                request.SetRequestHeader(
                    "Cache-Control",
                    "no-cache, no-store, max-age=0"
                );

                request.SetRequestHeader(
                    "Pragma",
                    "no-cache"
                );


                yield return request.SendWebRequest();


                if (
                    request.result ==
                    UnityWebRequest.Result.Success
                )
                {
                    string result =
                        request.downloadHandler.text;


                    if (
                        !string.IsNullOrWhiteSpace(
                            result
                        )
                    )
                    {
                        callback(
                            true,
                            result,
                            null
                        );


                        yield break;
                    }
                }


                lastError =
                    GetRequestError(
                        request
                    );


                // Don't retry permanent HTTP errors.
                if (
                    ShouldFailImmediately(
                        request
                    )
                )
                {
                    callback(
                        false,
                        null,
                        lastError
                    );


                    yield break;
                }
            }


            if (attempt < attempts)
            {
                float delay =
                    GetRetryDelay(
                        attempt
                    );


                if (delay > 0)
                {
                    yield return
                        new WaitForSecondsRealtime(
                            delay
                        );
                }
            }
        }


        callback(
            false,
            null,
            lastError
        );
    }


    // =========================================================
    // DOWNLOAD FILE
    // =========================================================

    public IEnumerator DownloadJobWithRetries(
        DownloadJob job,
        int completedJobs,
        int totalJobs)
    {
        int attempts =
            Mathf.Max(
                1,
                maxRetries + 1
            );


        string folder =
            Path.GetDirectoryName(
                job.finalPath
            );


        if (
            !Directory.Exists(
                folder
            )
        )
        {
            Directory.CreateDirectory(
                folder
            );
        }


        for (
            int attempt = 1;
            attempt <= attempts;
            attempt++
        )
        {
            job.success = false;


            TryDeleteFile(
                job.tempPath
            );


            SetStatus(
                "Downloading " +
                (completedJobs + 1) +
                "/" +
                totalJobs +
                (
                    attempt > 1
                        ?
                        " Retry " +
                        (attempt - 1) +
                        "/" +
                        maxRetries
                        :
                        ""
                )
            );


            bool succeeded =
                false;


            string error =
                "";


            bool failImmediately =
                false;


            using (
                UnityWebRequest request =
                    UnityWebRequest.Get(
                        job.image.url
                    )
            )
            {
                request.timeout =
                    timeoutSeconds;


                request.downloadHandler =
                    new DownloadHandlerFile(
                        job.tempPath
                    );


                UnityWebRequestAsyncOperation operation =
                    request.SendWebRequest();


                // Real-time progress.
                while (!operation.isDone)
                {
                    float current =
                        Mathf.Clamp01(
                            request.downloadProgress
                        );


                    float networkPhaseEnd =
                        Mathf.Clamp(
                            downloadPhaseEnd,
                            0.50f,
                            0.95f
                        );

                    downloadProgress =
                        networkPhaseEnd *
                        (
                            completedJobs +
                            current
                        ) /
                        totalJobs;


                    UpdateProgressUI();


                    yield return null;
                }


                succeeded =
                    request.result ==
                    UnityWebRequest.Result.Success;


                error =
                    GetRequestError(
                        request
                    );


                failImmediately =
                    ShouldFailImmediately(
                        request
                    );
            }


            // =================================================
            // SUCCESS
            // =================================================

            if (succeeded)
            {
                if (
                    ValidateFile(
                        job.tempPath,
                        job.image,
                        out string validationError
                    )
                )
                {
                    if (
                        PromoteTempFile(
                            job.tempPath,
                            job.finalPath
                        )
                    )
                    {
                        job.success = true;


                        yield break;
                    }


                    error =
                        "Could not move file into cache.";
                }
                else
                {
                    error =
                        validationError;
                }
            }


            // Never keep failed .part file.
            TryDeleteFile(
                job.tempPath
            );


            Debug.LogWarning(
                "[RemoteContent] Download failed: " +
                error
            );


            // 404 / 403 etc.
            // Don't waste time retrying.
            if (failImmediately)
            {
                job.success = false;

                yield break;
            }


            if (attempt < attempts)
            {
                float delay =
                    GetRetryDelay(
                        attempt
                    );


                if (delay > 0)
                {
                    yield return
                        new WaitForSecondsRealtime(
                            delay
                        );
                }
            }
        }


        job.success = false;
    }


    // =========================================================
    // DON'T RETRY PERMANENT HTTP ERRORS
    // =========================================================

    public bool ShouldFailImmediately(
        UnityWebRequest request)
    {
        if (request == null)
            return true;


        long code =
            request.responseCode;


        switch (code)
        {
            case 400:
            case 401:
            case 403:
            case 404:
            case 405:
            case 410:
            case 422:
                return true;
        }


        return false;
    }


    public string GetRequestError(
        UnityWebRequest request)
    {
        if (request == null)
            return "Unknown request error.";


        if (request.responseCode > 0)
        {
            return
                "HTTP " +
                request.responseCode +
                " - " +
                request.error;
        }


        return request.error;
    }


    // =========================================================
    // RETRY DELAY
    // =========================================================

    public float GetRetryDelay(
        int attempt)
    {
        float delay =
            retryDelaySeconds;


        if (exponentialBackoff)
        {
            delay *= Mathf.Pow(
                2f,
                attempt - 1
            );
        }


        return delay;
    }


    // =========================================================
    // BUILD DOWNLOAD QUEUE
    // =========================================================

    public bool BuildDownloadQueue(
        RemoteConfigData config,
        List<DownloadJob> queue,
        out string error)
    {
        error = null;


        HashSet<string> processed =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );


        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            if (
                binding == null ||
                string.IsNullOrWhiteSpace(
                    binding.sliderId
                )
            )
            {
                continue;
            }


            if (
                !processed.Add(
                    binding.sliderId
                )
            )
            {
                continue;
            }


            RemoteSlideData slide =
                FindSlide(
                    config,
                    binding.sliderId
                );


            if (
                slide == null ||
                slide.options == null
            )
            {
                continue;
            }


            for (
                int optionIndex = 0;
                optionIndex <
                slide.options.Length;
                optionIndex++
            )
            {
                RemoteOptionData option =
                    slide.options[
                        optionIndex
                    ];


                if (
                    option == null ||
                    option.images == null
                )
                {
                    continue;
                }


                foreach (
                    RemoteImageData image
                    in option.images
                )
                {
                    if (image == null)
                        continue;


                    if (
                        string.IsNullOrWhiteSpace(
                            image.key
                        )
                    )
                    {
                        error =
                            "Slide '" +
                            slide.id +
                            "' has an image with no key.";


                        return false;
                    }


                    image.version =
                        Mathf.Max(
                            1,
                            image.version
                        );


                    string path =
                        GetImagePath(
                            slide.id,
                            optionIndex,
                            image.key,
                            image.version
                        );


                    // Already cached.
                    if (
                        IsCachedFileValid(
                            path,
                            image
                        )
                    )
                    {
                        continue;
                    }


                    if (
                        string.IsNullOrWhiteSpace(
                            image.url
                        )
                    )
                    {
                        error =
                            slide.id +
                            " / " +
                            image.key +
                            " has no URL.";


                        return false;
                    }


                    DownloadJob job =
                        new DownloadJob
                        {
                            sliderId =
                                slide.id,

                            optionIndex =
                                optionIndex,

                            image =
                                image,

                            finalPath =
                                path,

                            tempPath =
                                path +
                                ".part"
                        };


                    queue.Add(
                        job
                    );
                }
            }
        }


        return AddStaticDownloads(config, queue, out error);
    }


    // =========================================================
    // VERIFY ALL REQUIRED FILES
    // =========================================================

    public bool AllRequiredFilesAvailable(
        RemoteConfigData config,
        out string error)
    {
        error = null;


        HashSet<string> processed =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );


        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            if (
                binding == null ||
                string.IsNullOrWhiteSpace(
                    binding.sliderId
                )
            )
            {
                continue;
            }


            if (
                !processed.Add(
                    binding.sliderId
                )
            )
            {
                continue;
            }


            RemoteSlideData slide =
                FindSlide(
                    config,
                    binding.sliderId
                );


            if (
                slide == null ||
                slide.options == null
            )
            {
                continue;
            }


            for (
                int optionIndex = 0;
                optionIndex <
                slide.options.Length;
                optionIndex++
            )
            {
                RemoteOptionData option =
                    slide.options[
                        optionIndex
                    ];


                if (
                    option == null ||
                    option.images == null
                )
                {
                    continue;
                }


                foreach (
                    RemoteImageData image
                    in option.images
                )
                {
                    if (image == null)
                    {
                        error =
                            slide.id +
                            " contains null image.";


                        return false;
                    }


                    string path =
                        GetImagePath(
                            slide.id,
                            optionIndex,
                            image.key,
                            Mathf.Max(
                                1,
                                image.version
                            )
                        );


                    if (
                        !IsCachedFileValid(
                            path,
                            image
                        )
                    )
                    {
                        error =
                            slide.id +
                            " / option " +
                            optionIndex +
                            " / " +
                            image.key +
                            " missing.";


                        return false;
                    }
                }
            }
        }


        return CheckStaticFiles(config, out error);
    }


    // =========================================================
    // APPLY CONFIG
    // =========================================================

    public void ApplyConfig(
        RemoteConfigData config)
    {
        if (config != null && config.enabled) ApplyStaticPanels(config);

        if (config == null)
            return;

        NotifyConfigApplied(config);


        if (!config.enabled)
        {
            // A cached OFF config from an older package must never blank or
            // disable the UI. Restore serialized Unity content instead.
            ApplyInspectorFallbacks();
            ReleaseStaticImages();
            return;
        }


        if (mainParent != null)
        {
            mainParent.SetActive(
                true
            );
        }


        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            if (binding == null)
                continue;


            RemoteSlideData slide =
                FindSlide(
                    config,
                    binding.sliderId
                );


            if (slide == null)
            {
                ApplyBindingFallback(
                    binding
                );


                continue;
            }


            if (
                binding.sliderParent != null
            )
            {
                binding.sliderParent.SetActive(
                    slide.enabled
                );
            }


            if (!slide.enabled)
            {
                ApplyFallbackImages(
                    binding
                );


                continue;
            }


            if (
                slide.options == null ||
                slide.options.Length == 0
            )
            {
                ApplyFallbackImages(
                    binding
                );


                continue;
            }


            int selected =
                Mathf.Clamp(
                    slide.selectedOption,
                    0,
                    slide.options.Length - 1
                );


            ApplyOptionImages(
                binding,
                slide.id,
                selected,
                slide.options[selected]
            );
        }
    }


    // =========================================================
    // APPLY OPTION
    // =========================================================

    public void ApplyOptionImages(
        RemoteSlideBinding binding,
        string sliderId,
        int optionIndex,
        RemoteOptionData option)
    {
        if (
            binding.imageElements == null
        )
        {
            return;
        }


        foreach (
            ImageElementBinding element
            in binding.imageElements
        )
        {
            if (
                element == null ||
                element.targetImage == null
            )
            {
                continue;
            }


            RemoteImageData remoteImage =
                FindImageByKey(
                    option,
                    element.key
                );


            if (remoteImage == null)
            {
                ApplyElementFallback(
                    element
                );


                continue;
            }


            string path =
                GetImagePath(
                    sliderId,
                    optionIndex,
                    remoteImage.key,
                    Mathf.Max(
                        1,
                        remoteImage.version
                    )
                );


            if (
                TryLoadElementImage(
                    element,
                    path
                )
            )
            {
                continue;
            }


            string older =
                FindNewestCachedVersion(
                    sliderId,
                    optionIndex,
                    remoteImage.key
                );


            if (
                !string.IsNullOrEmpty(
                    older
                ) &&
                TryLoadElementImage(
                    element,
                    older
                )
            )
            {
                continue;
            }


            ApplyElementFallback(
                element
            );
        }
    }


    // =========================================================
    // DYNAMIC VISUAL COMMIT BARRIER
    // =========================================================

    public bool ApplyDynamicSlidesImmediately(
        RemoteConfigData config,
        out string error)
    {
        error = null;

        if (config == null || !config.enabled)
            return true;

        if (slides == null)
            return true;

        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            if (
                binding == null ||
                string.IsNullOrWhiteSpace(
                    binding.sliderId
                )
            )
            {
                continue;
            }

            RemoteSlideData slide =
                FindSlide(
                    config,
                    binding.sliderId
                );

            if (slide == null)
                continue;

            if (binding.sliderParent != null)
            {
                binding.sliderParent.SetActive(
                    slide.enabled
                );
            }

            if (!slide.enabled)
                continue;

            if (
                slide.options == null ||
                slide.options.Length == 0
            )
            {
                continue;
            }

            int selected =
                Mathf.Clamp(
                    slide.selectedOption,
                    0,
                    slide.options.Length - 1
                );

            RemoteOptionData option =
                slide.options[selected];

            if (
                option == null ||
                binding.imageElements == null
            )
            {
                continue;
            }

            foreach (
                ImageElementBinding element
                in binding.imageElements
            )
            {
                if (
                    element == null ||
                    element.targetImage == null
                )
                {
                    continue;
                }

                RemoteImageData remoteImage =
                    FindImageByKey(
                        option,
                        element.key
                    );

                // A missing key means this element intentionally keeps its
                // local/fallback presentation. It is not a commit error.
                if (remoteImage == null)
                    continue;

                string expectedPath =
                    GetImagePath(
                        slide.id,
                        selected,
                        remoteImage.key,
                        Mathf.Max(
                            1,
                            remoteImage.version
                        )
                    );

                if (
                    !IsCachedFileValid(
                        expectedPath,
                        remoteImage
                    )
                )
                {
                    error =
                        slide.id +
                        "/" +
                        remoteImage.key +
                        ": exact cached image is missing or invalid.";

                    return false;
                }

                if (
                    !TryLoadElementImage(
                        element,
                        expectedPath
                    )
                )
                {
                    error =
                        slide.id +
                        "/" +
                        remoteImage.key +
                        ": cached image could not be applied to the target Image.";

                    return false;
                }
            }
        }

        return VerifyDynamicSlidesApplied(
            config,
            out error
        );
    }


    public bool VerifyDynamicSlidesApplied(
        RemoteConfigData config,
        out string error)
    {
        error = null;

        if (config == null || !config.enabled)
            return true;

        if (slides == null)
            return true;

        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            if (
                binding == null ||
                string.IsNullOrWhiteSpace(
                    binding.sliderId
                )
            )
            {
                continue;
            }

            RemoteSlideData slide =
                FindSlide(
                    config,
                    binding.sliderId
                );

            if (slide == null)
                continue;

            if (
                binding.sliderParent != null &&
                binding.sliderParent.activeSelf !=
                slide.enabled
            )
            {
                error =
                    slide.id +
                    ": slider parent active state was overwritten before reveal.";

                return false;
            }

            if (!slide.enabled)
                continue;

            if (
                slide.options == null ||
                slide.options.Length == 0
            )
            {
                continue;
            }

            int selected =
                Mathf.Clamp(
                    slide.selectedOption,
                    0,
                    slide.options.Length - 1
                );

            RemoteOptionData option =
                slide.options[selected];

            if (
                option == null ||
                binding.imageElements == null
            )
            {
                continue;
            }

            foreach (
                ImageElementBinding element
                in binding.imageElements
            )
            {
                if (
                    element == null ||
                    element.targetImage == null
                )
                {
                    continue;
                }

                RemoteImageData remoteImage =
                    FindImageByKey(
                        option,
                        element.key
                    );

                if (remoteImage == null)
                    continue;

                string expectedPath =
                    GetImagePath(
                        slide.id,
                        selected,
                        remoteImage.key,
                        Mathf.Max(
                            1,
                            remoteImage.version
                        )
                    );

                if (
                    !IsCachedFileValid(
                        expectedPath,
                        remoteImage
                    )
                )
                {
                    error =
                        slide.id +
                        "/" +
                        remoteImage.key +
                        ": exact cached image is no longer valid.";

                    return false;
                }

                if (
                    element.runtimeSprite == null ||
                    element.runtimeTexture == null ||
                    !string.Equals(
                        element.loadedPath,
                        expectedPath,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    element.targetImage.sprite !=
                    element.runtimeSprite
                )
                {
                    error =
                        slide.id +
                        "/" +
                        remoteImage.key +
                        ": sprite has not committed to the target Image yet.";

                    return false;
                }
            }
        }

        return true;
    }


    // =========================================================
    // LOAD LOCAL IMAGE
    // =========================================================

    public bool TryLoadElementImage(
        ImageElementBinding element,
        string path)
    {
        if (
            element == null ||
            element.targetImage == null ||
            !File.Exists(path)
        )
        {
            return false;
        }


        CaptureOriginalSpriteIfNeeded(
            element
        );


        // Already loaded.
        if (
            element.runtimeSprite != null &&
            element.runtimeTexture != null &&
            element.loadedPath == path
        )
        {
            element.targetImage.sprite =
                element.runtimeSprite;


            return true;
        }


        try
        {
            byte[] bytes =
                File.ReadAllBytes(
                    path
                );


            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );


            if (
                !texture.LoadImage(
                    bytes,
                    true
                )
            )
            {
                Destroy(
                    texture
                );


                return false;
            }


            DestroyElementRuntimeImage(
                element
            );


            element.runtimeTexture =
                texture;


            element.runtimeSprite =
                Sprite.Create(
                    texture,

                    new Rect(
                        0,
                        0,
                        texture.width,
                        texture.height
                    ),

                    new Vector2(
                        0.5f,
                        0.5f
                    ),

                    pixelsPerUnit
                );


            element.loadedPath =
                path;


            element.targetImage.sprite =
                element.runtimeSprite;


            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "[RemoteContent] Image load failed: " +
                e.Message
            );


            return false;
        }
    }


    // =========================================================
    // FALLBACK
    // =========================================================

    public void ApplyInspectorFallbacks()
    {
        if (mainParent != null)
        {
            mainParent.SetActive(
                true
            );
        }


        foreach (
            RemoteSlideBinding binding
            in slides
        )
        {
            ApplyBindingFallback(
                binding
            );
        }
    }


    private void CaptureOriginalSlideSprites()
    {
        if (slides == null)
            return;

        foreach (RemoteSlideBinding binding in slides)
        {
            if (binding == null || binding.imageElements == null)
                continue;

            foreach (ImageElementBinding element in binding.imageElements)
            {
                CaptureOriginalSpriteIfNeeded(element);
            }
        }
    }


    public void ApplyBindingFallback(
        RemoteSlideBinding binding)
    {
        if (binding == null)
            return;


        if (
            binding.sliderParent != null
        )
        {
            binding.sliderParent.SetActive(
                binding.defaultEnabled
            );
        }


        ApplyFallbackImages(
            binding
        );
    }


    public void ApplyFallbackImages(
        RemoteSlideBinding binding)
    {
        if (
            binding == null ||
            binding.imageElements == null
        )
        {
            return;
        }


        foreach (
            ImageElementBinding element
            in binding.imageElements
        )
        {
            ApplyElementFallback(
                element
            );
        }
    }


    public void ApplyElementFallback(
        ImageElementBinding element)
    {
        if (
            element == null ||
            element.targetImage == null
        )
        {
            return;
        }


        CaptureOriginalSpriteIfNeeded(
            element
        );


        Sprite localSprite =
            element.fallbackSprite != null
                ? element.fallbackSprite
                : element.originalSprite;

        // No local sprite is known yet. Keep the current valid saved remote
        // sprite (or whatever another local owner assigned) instead of
        // destroying it and deliberately producing an empty Image.
        if (localSprite == null || localSprite == element.runtimeSprite)
            return;

        element.targetImage.sprite = localSprite;

        DestroyElementRuntimeImage(
            element
        );
    }


    private void CaptureOriginalSpriteIfNeeded(
        ImageElementBinding element)
    {
        if (element == null || element.targetImage == null)
            return;

        Sprite current = element.targetImage.sprite;

        // Null is not a fallback and is never considered captured. A local
        // owner may assign its sprite later in Start or during runtime.
        if (
            current == null ||
            current == element.runtimeSprite ||
            (
                element.originalSpriteCaptured &&
                element.originalSprite != null
            )
        )
        {
            return;
        }

        element.originalSprite = current;

        element.originalSpriteCaptured = true;
    }


    private void FinishRemoteConfigDisabled()
    {
        if (useLocalContentWhenRemoteConfigDisabled)
        {
            if (activeConfig != null && activeConfig.enabled)
            {
                ApplyConfig(activeConfig);
                state = RemoteContentState.OfflineReady;
                SetStatus("Remote config is off. Using saved content.");
            }
            else
            {
                activeConfig = null;
                ApplyInspectorFallbacks();
                ReleaseStaticImages();
                state = RemoteContentState.OfflineReady;
                SetStatus("Remote config is off. Using built-in content.");
            }
        }
        else
        {
            state = RemoteContentState.Ready;
            SetStatus("Remote config is off. Continuing.");
            FinishStaticPanelSequence(
                null,
                "Remote config is off; finishing the static panel sequence."
            );
        }

        downloadProgress = 1f;
        UpdateProgressUI();
        SetDownloadUIState(false, false);

        // Clear this before hiding the loading root in case it owns us.
        isBusy = false;

        if (loadingParent != null)
            loadingParent.SetActive(false);
    }


    // =========================================================
    // DESTROY RUNTIME IMAGES
    // =========================================================

    public void DestroySlideRuntimeImages(
        RemoteSlideBinding binding)
    {
        if (
            binding == null ||
            binding.imageElements == null
        )
        {
            return;
        }


        foreach (
            ImageElementBinding element
            in binding.imageElements
        )
        {
            DestroyElementRuntimeImage(
                element
            );
        }
    }


    public void DestroyElementRuntimeImage(
        ImageElementBinding element)
    {
        if (element == null)
            return;


        if (
            element.runtimeSprite != null
        )
        {
            Destroy(
                element.runtimeSprite
            );


            element.runtimeSprite =
                null;
        }


        if (
            element.runtimeTexture != null
        )
        {
            Destroy(
                element.runtimeTexture
            );


            element.runtimeTexture =
                null;
        }


        element.loadedPath =
            null;
    }


    // =========================================================
    // LOOKUPS
    // =========================================================

    public RemoteSlideData FindSlide(
        RemoteConfigData config,
        string id)
    {
        if (
            config == null ||
            config.sliders == null
        )
        {
            return null;
        }


        foreach (
            RemoteSlideData slide
            in config.sliders
        )
        {
            if (
                slide != null &&
                string.Equals(
                    slide.id,
                    id,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return slide;
            }
        }


        return null;
    }


    public RemoteImageData FindImageByKey(
        RemoteOptionData option,
        string key)
    {
        if (
            option == null ||
            option.images == null
        )
        {
            return null;
        }


        foreach (
            RemoteImageData image
            in option.images
        )
        {
            if (
                image != null &&
                string.Equals(
                    image.key,
                    key,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return image;
            }
        }


        return null;
    }


    // =========================================================
    // CACHE PATH
    // =========================================================

    public string GetImagePath(
        string sliderId,
        int optionIndex,
        string key,
        int version)
    {
        return Path.Combine(
            CacheRoot,

            MakeSafeFileName(
                sliderId
            ),

            "option_" +
            optionIndex,

            MakeSafeFileName(
                key
            ) +
            "_v" +
            version +
            ".cache"
        );
    }


    public string FindNewestCachedVersion(
        string sliderId,
        int optionIndex,
        string key)
    {
        string folder =
            Path.Combine(
                CacheRoot,

                MakeSafeFileName(
                    sliderId
                ),

                "option_" +
                optionIndex
            );


        if (
            !Directory.Exists(
                folder
            )
        )
        {
            return null;
        }


        string safeKey =
            MakeSafeFileName(
                key
            );


        string[] files =
            Directory.GetFiles(
                folder,
                safeKey +
                "_v*.cache"
            );


        if (files.Length == 0)
            return null;


        string newest =
            null;


        int bestVersion =
            int.MinValue;


        foreach (
            string file
            in files
        )
        {
            string name =
                Path.GetFileNameWithoutExtension(
                    file
                );


            int marker =
                name.LastIndexOf(
                    "_v",
                    StringComparison.Ordinal
                );


            if (marker < 0)
                continue;


            string versionText =
                name.Substring(
                    marker + 2
                );


            if (
                int.TryParse(
                    versionText,
                    out int version
                ) &&
                version >
                bestVersion
            )
            {
                bestVersion =
                    version;


                newest =
                    file;
            }
        }


        return newest;
    }


    // =========================================================
    // FILE VALIDATION
    // =========================================================

    public bool ValidateFile(
        string path,
        RemoteImageData image,
        out string error)
    {
        error = null;


        if (!File.Exists(path))
        {
            error =
                "File does not exist.";


            return false;
        }


        try
        {
            FileInfo info =
                new FileInfo(
                    path
                );


            if (info.Length <= 0)
            {
                error =
                    "File is empty.";


                return false;
            }


            if (
                validateFileSize &&
                image != null &&
                image.size > 0 &&
                info.Length !=
                image.size
            )
            {
                error =
                    "File size mismatch.";


                return false;
            }


            if (
                validateSha256 &&
                image != null &&
                !string.IsNullOrWhiteSpace(
                    image.sha256
                )
            )
            {
                string actual =
                    ComputeSha256(
                        path
                    );


                string expected =
                    image.sha256
                        .Replace(
                            " ",
                            ""
                        )
                        .Replace(
                            "-",
                            ""
                        )
                        .Trim();


                if (
                    !string.Equals(
                        actual,
                        expected,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    error =
                        "SHA256 mismatch.";


                    return false;
                }
            }


            return true;
        }
        catch (Exception e)
        {
            error =
                e.Message;


            return false;
        }
    }


    public bool IsCachedFileValid(
        string path,
        RemoteImageData image)
    {
        return ValidateFile(
            path,
            image,
            out _
        );
    }


    public string ComputeSha256(
        string path)
    {
        using (
            SHA256 sha =
                SHA256.Create()
        )
        using (
            FileStream stream =
                File.OpenRead(
                    path
                )
        )
        {
            byte[] hash =
                sha.ComputeHash(
                    stream
                );


            StringBuilder builder =
                new StringBuilder();


            foreach (
                byte value
                in hash
            )
            {
                builder.Append(
                    value.ToString(
                        "x2"
                    )
                );
            }


            return builder.ToString();
        }
    }


    // =========================================================
    // PROMOTE DOWNLOAD
    // =========================================================

    public bool PromoteTempFile(
        string temp,
        string final)
    {
        try
        {
            string folder =
                Path.GetDirectoryName(
                    final
                );


            if (
                !Directory.Exists(
                    folder
                )
            )
            {
                Directory.CreateDirectory(
                    folder
                );
            }


            if (
                File.Exists(
                    final
                )
            )
            {
                File.Delete(
                    final
                );
            }


            File.Move(
                temp,
                final
            );


            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[RemoteContent] Could not promote file: " +
                e.Message
            );


            return false;
        }
    }


    // =========================================================
    // PROMOTE CONFIG
    // =========================================================

    public bool PromotePendingConfig(
        string json)
    {
        try
        {
            string temp =
                ActiveConfigPath +
                ".new";


            File.WriteAllText(
                temp,
                json
            );


            if (
                File.Exists(
                    ActiveConfigPath
                )
            )
            {
                File.Delete(
                    ActiveConfigPath
                );
            }


            File.Move(
                temp,
                ActiveConfigPath
            );


            TryDeleteFile(
                PendingConfigPath
            );


            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[RemoteContent] Config activation failed: " +
                e.Message
            );


            return false;
        }
    }


    // =========================================================
    // CONFIG VALIDATION
    // =========================================================

    public bool ValidateConfig(
        RemoteConfigData config,
        out string error)
    {
        error = null;


        if (config == null)
        {
            error =
                "Config is null.";


            return false;
        }


        if (config.sliders == null)
        {
            config.sliders =
                new RemoteSlideData[0];
        }


        HashSet<string> slideIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );


        foreach (
            RemoteSlideData slide
            in config.sliders
        )
        {
            if (slide == null)
            {
                error =
                    "Null slide entry.";


                return false;
            }


            if (
                string.IsNullOrWhiteSpace(
                    slide.id
                )
            )
            {
                error =
                    "Slide has no ID.";


                return false;
            }


            if (
                !slideIds.Add(
                    slide.id
                )
            )
            {
                error =
                    "Duplicate slide ID: " +
                    slide.id;


                return false;
            }


            if (slide.options == null)
            {
                slide.options =
                    new RemoteOptionData[0];
            }


            if (
                slide.options.Length > 0 &&
                (
                    slide.selectedOption < 0 ||
                    slide.selectedOption >=
                    slide.options.Length
                )
            )
            {
                error =
                    "Slide '" +
                    slide.id +
                    "' selectedOption " +
                    slide.selectedOption +
                    " is outside options range.";


                return false;
            }


            for (
                int optionIndex = 0;
                optionIndex <
                slide.options.Length;
                optionIndex++
            )
            {
                RemoteOptionData option =
                    slide.options[
                        optionIndex
                    ];


                if (option == null)
                {
                    error =
                        "Slide '" +
                        slide.id +
                        "' option " +
                        optionIndex +
                        " is null.";


                    return false;
                }


                if (option.images == null)
                {
                    option.images =
                        new RemoteImageData[0];
                }


                HashSet<string> keys =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase
                    );


                foreach (
                    RemoteImageData image
                    in option.images
                )
                {
                    if (image == null)
                    {
                        error =
                            "Slide '" +
                            slide.id +
                            "' has null image.";


                        return false;
                    }


                    if (
                        string.IsNullOrWhiteSpace(
                            image.key
                        )
                    )
                    {
                        error =
                            "Slide '" +
                            slide.id +
                            "' option " +
                            optionIndex +
                            " has image with no key.";


                        return false;
                    }


                    if (
                        !keys.Add(
                            image.key
                        )
                    )
                    {
                        error =
                            "Duplicate key '" +
                            image.key +
                            "' in slide '" +
                            slide.id +
                            "' option " +
                            optionIndex;


                        return false;
                    }


                    if (image.version <= 0)
                    {
                        image.version = 1;
                    }
                }
            }
        }


        return ValidateStaticConfig(config, out error);
    }


    // =========================================================
    // JSON
    // =========================================================

    public RemoteConfigData ParseConfig(
        string json)
    {
        if (
            string.IsNullOrWhiteSpace(
                json
            )
        )
        {
            return null;
        }


        try
        {
            RemoteConfigData config = JsonUtility.FromJson<RemoteConfigData>(json);
            // Preserve missing/null optional images in static elements. Dynamic data
            // continues to use JsonUtility and its existing validation unchanged.
            if (config != null) ParseStaticPanels(json, config);
            return config;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[RemoteContent] JSON error: " +
                e.Message
            );


            return null;
        }
    }


    public RemoteConfigData LoadConfigFromFile(
        string path)
    {
        if (!File.Exists(path))
            return null;


        try
        {
            string json =
                File.ReadAllText(
                    path
                );


            RemoteConfigData config =
                ParseConfig(
                    json
                );


            if (
                ValidateConfig(
                    config,
                    out _
                )
            )
            {
                return config;
            }
        }
        catch
        {
        }


        return null;
    }


    public bool SafeWriteText(
        string path,
        string value)
    {
        try
        {
            string folder =
                Path.GetDirectoryName(
                    path
                );


            if (
                !Directory.Exists(
                    folder
                )
            )
            {
                Directory.CreateDirectory(
                    folder
                );
            }


            File.WriteAllText(
                path,
                value
            );


            return true;
        }
        catch
        {
            return false;
        }
    }


    // =========================================================
    // DELETE PART FILES
    // =========================================================

    public void DeleteStalePartFiles()
    {
        try
        {
            if (
                !Directory.Exists(
                    CacheRoot
                )
            )
            {
                return;
            }


            string[] files =
                Directory.GetFiles(
                    CacheRoot,
                    "*.part",
                    SearchOption.AllDirectories
                );


            foreach (
                string file
                in files
            )
            {
                TryDeleteFile(
                    file
                );
            }
        }
        catch
        {
        }
    }


    public void TryDeleteFile(
        string path)
    {
        try
        {
            if (
                File.Exists(
                    path
                )
            )
            {
                File.Delete(
                    path
                );
            }
        }
        catch
        {
        }
    }


    public string MakeSafeFileName(
        string value)
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return "unnamed";
        }


        foreach (
            char invalid
            in Path.GetInvalidFileNameChars()
        )
        {
            value =
                value.Replace(
                    invalid,
                    '_'
                );
        }


        return value
            .Replace(
                "/",
                "_"
            )
            .Replace(
                "\\",
                "_"
            )
            .Replace(
                ":",
                "_"
            );
    }


    // =========================================================
    // FAILURE
    // =========================================================

    public void FinishWithFailure(
        string message)
    {
        Debug.LogWarning(
            "[RemoteContent] " +
            message
        );


        // =====================================================
        // USE LAST WORKING CACHE IF AVAILABLE
        // =====================================================

        if (activeConfig != null)
        {
            ApplyConfig(
                activeConfig
            );


            state =
                RemoteContentState.OfflineReady;


            SetStatus(
                message +
                "\nUsing saved content."
            );
        }

        // =====================================================
        // OTHERWISE USE UNITY FALLBACK CONTENT
        // =====================================================

        else
        {
            ApplyInspectorFallbacks();


            state =
                RemoteContentState.Failed;


            SetStatus(
                message +
                "\nUsing built-in content."
            );
        }


        // =====================================================
        // FAILURE UI
        // =====================================================

        if (showRetryButtonOnFailure)
        {
            SetDownloadUIState(
                false,
                true
            );
        }
        else
        {
            SetDownloadUIState(
                false,
                false
            );
        }


        // Clear this before hiding UI. Deactivating a parent stops this
        // controller's coroutine, so callers may never reach their cleanup.
        isBusy = false;

        // Don't block the game.
        if (
            hideLoadingPanelOnFailure &&
            loadingParent != null
        )
        {
            loadingParent.SetActive(
                false
            );
        }
    }


    // =========================================================
    // UI
    // =========================================================

    public void SetDownloadUIState(
        bool showProgress,
        bool showRetry)
    {
        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(
                showProgress
            );
        }


        if (progressText != null)
        {
            progressText.gameObject.SetActive(
                showProgress
            );
        }


        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(
                showRetry
            );
        }
    }


    public void SetStatus(
        string text)
    {
        if (statusText != null)
        {
            statusText.text =
                text;
        }
    }


    public void ResetProgress()
    {
        downloadProgress = 0f;

        totalFilesToDownload = 0;

        downloadedFiles = 0;

        failedFiles = 0;


        UpdateProgressUI();
    }


    public void UpdateProgressUI()
    {
        downloadProgress =
            Mathf.Clamp01(
                downloadProgress
            );


        if (progressSlider != null)
        {
            progressSlider.value =
                downloadProgress;
        }


        if (progressText != null)
        {
            int percent =
                Mathf.RoundToInt(
                    downloadProgress *
                    100f
                );


            if (
                totalFilesToDownload > 0
            )
            {
                progressText.text =
                    percent +
                    "%  " +
                    downloadedFiles +
                    "/" +
                    totalFilesToDownload;
            }
            else
            {
                progressText.text =
                    percent +
                    "%";
            }
        }
    }
}
