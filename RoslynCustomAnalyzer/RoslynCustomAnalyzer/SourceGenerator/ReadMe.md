### Advanced

This is code gen for ECS VFX system based on space fight example from unity, if you interested, please contact me and I will finish this readme file.

```csharp
[GenerateVFXSystem("Explosions", VFXSystemType.Parentless, 10000)]
[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXExplosionsRequest
{
    public Vector3 Position;
    public float Scale;
}
```