using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseOwl.Casc.Diablo4;

/// <summary>The eight Diablo IV character classes, for
/// <see cref="Diablo4Storage.ReadSkillTree(SkillTreeClass, string)"/>.</summary>
public enum SkillTreeClass
{
    /// <summary>Barbarian.</summary>
    Barbarian,
    /// <summary>Druid.</summary>
    Druid,
    /// <summary>Necromancer.</summary>
    Necromancer,
    /// <summary>Paladin.</summary>
    Paladin,
    /// <summary>Rogue.</summary>
    Rogue,
    /// <summary>Sorcerer.</summary>
    Sorcerer,
    /// <summary>Spiritborn.</summary>
    Spiritborn,
    /// <summary>Warlock.</summary>
    Warlock,
}

/// <summary>The role of a <see cref="SkillTreeNode"/> in the tree.</summary>
public enum SkillTreeNodeKind
{
    /// <summary>Any node type not otherwise classified.</summary>
    Other,
    /// <summary>An active-skill unlock node (the skill itself).</summary>
    Unlock,
    /// <summary>A skill-rank node.</summary>
    SkillRank,
    /// <summary>A modifier/upgrade node (enhancement / choice).</summary>
    Modifier,
    /// <summary>A passive talent node.</summary>
    Talent,
}

/// <summary>
/// A decoded Diablo IV per-class <b>skill tree</b> (#57, CL-111) — the logical
/// per-node structure the game builds from <c>SkillTreeRewards</c> (g20/547685)
/// and the class board (g39). Exposes each node's kind, the skill it is/modifies,
/// and its mutually-exclusive modifier group; the class's active skills; and a
/// modifier-group membership lookup.
/// </summary>
/// <remarks>
/// <para>This is the <b>logical</b> tree (per the consumer's shape input on #57):
/// what each node is, what it modifies, and the "pick one per group" rule. The
/// visual graph — X/Y layout positions, edges, connector routing — lives in the
/// g39 board record and is intentionally <b>not</b> surfaced here (no renderer
/// consumer). A node's <b>effect text</b> is resolved through its
/// <see cref="SkillTreeNode.SkillSno"/>: read that Power
/// (<see cref="Diablo4Storage.ReadPower(int, string)"/>) for the skill's
/// description and its <see cref="PowerDefinition.Modifiers"/> (the modifier
/// name/description list).</para>
/// <para><b>Scope (v1).</b> Node kind, skill, and modifier group are decoded from
/// <c>SkillTreeRewards</c> (byte-verified). The tier/category <i>cluster</i>
/// grouping (Basic / Core / … / Ultimate) is derived from the g39 board topology
/// and is a follow-up — see the #57 thread.</para>
/// </remarks>
public sealed class SkillTree
{
    private readonly SkillTreeNode[] _nodes;
    private readonly int[] _skills;

    internal SkillTree(SkillTreeClass @class, SkillTreeNode[] nodes, int[] skills)
    {
        Class = @class;
        _nodes = nodes;
        _skills = skills;
    }

    /// <summary>The class this tree belongs to.</summary>
    public SkillTreeClass Class { get; }

    /// <summary>Every node in the class's tree — unlock / skill-rank / modifier /
    /// talent nodes (see <see cref="SkillTreeNode"/>).</summary>
    public IReadOnlyList<SkillTreeNode> Nodes => _nodes;

    /// <summary>The class's <b>active-skill</b> Power SNO ids (the g39 board's
    /// skill list). Read each with
    /// <see cref="Diablo4Storage.ReadPower(int, string)"/>.</summary>
    public IReadOnlyList<int> Skills => _skills;

    /// <summary>
    /// The mutually-exclusive modifier set for a skill's group — the "you may pick
    /// only one of these" rule as data. Returns every <see cref="SkillTreeNodeKind.Modifier"/>
    /// node with the same <paramref name="skillSno"/> and
    /// <paramref name="groupId"/> (e.g. a skill's <c>UpgradeA/B/C</c> share a
    /// group; its <c>Side1..4</c> share another). Empty if none match.
    /// </summary>
    /// <param name="skillSno">The modified skill's Power SNO
    /// (<see cref="SkillTreeNode.SkillSno"/>).</param>
    /// <param name="groupId">The modifier group id
    /// (<see cref="SkillTreeNode.ModifierGroupId"/>).</param>
    public IReadOnlyList<SkillTreeNode> ModifierGroup(int skillSno, int groupId) =>
        _nodes.Where(n => n.Kind == SkillTreeNodeKind.Modifier
                          && n.SkillSno == skillSno && n.ModifierGroupId == groupId).ToArray();
}

/// <summary>
/// One node of a <see cref="SkillTree"/> (#57, CL-111) — a skill unlock, a
/// skill-rank, a modifier/upgrade, or a passive talent.
/// </summary>
public sealed class SkillTreeNode
{
    internal SkillTreeNode(string name, SkillTreeNodeKind kind, int skillSno, int modifierGroupId)
    {
        Name = name;
        Kind = kind;
        SkillSno = skillSno;
        ModifierGroupId = modifierGroupId;
    }

    /// <summary>The node's CoreTOC-style key (e.g. <c>"Rogue_Unlock_BladeShift"</c>,
    /// <c>"Rogue_Mod_BladeShift_UpgradeA"</c>) — a durable, opaque stable id.</summary>
    public string Name { get; }

    /// <summary>The node's role (unlock / skill-rank / modifier / talent).</summary>
    public SkillTreeNodeKind Kind { get; }

    /// <summary>The Power SNO of the skill this node <b>is</b> (for an
    /// <see cref="SkillTreeNodeKind.Unlock"/>) or <b>modifies</b> (for a
    /// <see cref="SkillTreeNodeKind.Modifier"/>). This is also the node's
    /// <b>prerequisite</b> — a modifier needs its skill. <b>0</b> when the node
    /// references no skill. Resolve effect text via
    /// <see cref="Diablo4Storage.ReadPower(int, string)"/>.</summary>
    public int SkillSno { get; }

    /// <summary>For a <see cref="SkillTreeNodeKind.Modifier"/> node, its
    /// <b>mutually-exclusive group id</b> (modifiers sharing a
    /// <c>(SkillSno, ModifierGroupId)</c> are one "pick-one" set — enumerate with
    /// <see cref="SkillTree.ModifierGroup(int, int)"/>). <b>-1</b> for non-modifier
    /// nodes.</summary>
    public int ModifierGroupId { get; }
}
