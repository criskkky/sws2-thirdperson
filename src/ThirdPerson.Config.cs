namespace ThirdPerson;

public class ThirdPersonConfig
{
    public string CustomTPCommand { get; set; } = "tp";

    public string UseTpPermission { get; set; } = "thirdperson.use";

    public bool UseSmoothCam { get; set; } = true;

    public float ThirdPersonDistance { get; set; } = 100f;

    public float ThirdPersonHeight { get; set; } = 80f;

    public float SmoothCameraSpeed { get; set; } = 0.3f;

    public string DamageMode { get; set; } = "back";

    public bool EnableKnifeWarnings { get; set; } = true;
}
