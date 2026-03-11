
// -----------------------------------------------------------------------------
// WARNING: This is auto-generated file! DO NOT MODIFY
// Generated from type: [FrooxEngine]FrooxEngine.PhotonDust.ColorByVelocityDirectionSH2
// Generated on: středa 11. března 2026 18:42:10
// Resonite version: 2026.3.11.1110
// Resonite Link Version: 0.13.0.0
// -----------------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FrooxEngine.PhotonDust
{
    [Serializable]
[ResoniteTypeName("[FrooxEngine]FrooxEngine.PhotonDust.ColorByVelocityDirectionSH2")]
public partial class ColorByVelocityDirectionSH2 : global::FrooxEngine.PhotonDust.ParticleSystemModule<global::System.Object>

{
    public UnityEngine.Rendering.SphericalHarmonicsL2 SH { get => SH_Element.Data; set => SH_Element.Data = value; }
public Field<global::FrooxEngine.Sync<UnityEngine.Rendering.SphericalHarmonicsL2>, UnityEngine.Rendering.SphericalHarmonicsL2> SH_Element = new();

public override void CollectMembers(
    System.Collections.Generic.Dictionary<string, ResoniteLink.Member> members, IConversionContext context)
{
    base.CollectMembers(members, context);
members.Add("SH", SH_Element.ToLinkField(context));
}

}
}
