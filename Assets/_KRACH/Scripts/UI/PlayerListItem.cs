using DG.Tweening;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    public string playerName;
    public int connectionID;
    public ulong playerSteamID;
    public bool avatarReceived;

    public TextMeshProUGUI playerNameText;
    public PlayerRole role;
    public TextMeshProUGUI playerRole;
    public RawImage playerIcon;

    public TextMeshProUGUI readyStatusText;
    public Color colorForReadyText;
    public Color colorForGettingReadyText;
    public Image isReadiedImage;
    public bool isReady;

    protected Callback<AvatarImageLoaded_t> imageLoaded;

    private bool? lastShownReady;
    private Texture2D runtimeAvatar; // created by us → released by us

    private void Start()
    {
        if (SteamManager.Initialized)
            imageLoaded = Callback<AvatarImageLoaded_t>.Create(OnImageLoaded);
    }

    private void OnDestroy()
    {
        imageLoaded?.Dispose();

        // Steam avatars are textures created at runtime – without Destroy they stay in memory.
        if (runtimeAvatar != null) Destroy(runtimeAvatar);
    }

    void GetPlayerIcon()
    {
        if (!SteamManager.Initialized || playerSteamID == 0) return;

        int imageID = SteamFriends.GetLargeFriendAvatar((CSteamID)playerSteamID);
        if (imageID == -1) { return; } // is loaded later → OnImageLoaded
        SetAvatar(GetSteamImageAsTexture(imageID));
    }

    public void SetPlayerValues(PlayerRole role)
    {
        this.role = role; // store the role
        playerNameText.text = playerName;
        playerRole.text = role.ToString() + " •"; // the bullet was a broken character before
        playerIcon.uvRect = new Rect(0, 1, 1, -1); // Steam delivers the texture vertically flipped
        if (!avatarReceived) { GetPlayerIcon(); }
        UpdateReadyStatusText();
    }

    public void UpdateReadyStatusText()
    {
        readyStatusText.text = isReady ? "Ready!" : "Getting ready...";
        readyStatusText.color = isReady ? colorForReadyText : colorForGettingReadyText;
        isReadiedImage.enabled = isReady;

        // Only animate on an actual change, not on every list refresh.
        bool changed = lastShownReady.HasValue && lastShownReady.Value != isReady;
        lastShownReady = isReady;

        if (changed && isReady && GameFeelSettings.Instance.lobbyReadyPop)
        {
            Transform t = isReadiedImage.transform;
            t.DOKill(true);
            t.DOPunchScale(Vector3.one * 0.5f, 0.4f, 6, 0.5f).SetLink(gameObject);

            readyStatusText.rectTransform.DOKill(true);
            readyStatusText.rectTransform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f).SetLink(gameObject);
        }
    }

    private void OnImageLoaded(AvatarImageLoaded_t callback)
    {
        if (callback.m_steamID.m_SteamID != playerSteamID) return;
        SetAvatar(GetSteamImageAsTexture(callback.m_iImage));
    }

    private void SetAvatar(Texture2D texture)
    {
        if (texture == null || playerIcon == null) return;

        // Release the previous runtime texture (the callback can arrive multiple times).
        if (runtimeAvatar != null && runtimeAvatar != texture) Destroy(runtimeAvatar);

        runtimeAvatar = texture;
        playerIcon.texture = texture;
    }

    private Texture2D GetSteamImageAsTexture(int iImage)
    {
        Texture2D texture = null;
        bool isValid = SteamUtils.GetImageSize(iImage, out uint width, out uint height);

        if (isValid)
        {
            byte[] image = new byte[width * height * 4];
            isValid = SteamUtils.GetImageRGBA(iImage, image, (int)(width * height * 4));

            if (isValid)
            {
                texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, true);
                texture.LoadRawTextureData(image);
                texture.Apply();
            }
        }

        avatarReceived = texture != null;
        return texture;
    }
}
