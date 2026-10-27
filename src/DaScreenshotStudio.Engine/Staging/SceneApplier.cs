using System.Runtime.InteropServices;
using System.Text;
using DaScreenshotStudio.Assets.Characters;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;
using HumanAppearance = DaScreenshotStudio.Assets.Characters.HumanAppearance;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.World;

namespace DaScreenshotStudio.Engine.Staging;

public static class SceneApplier
{
    public const uint StageTime = 1000;

    public static readonly Encoding TextEncoding = Encoding.Latin1;

    public static void Apply(SceneHost host, SceneDocument scene, MapCatalog? maps = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);

        ApplyMap(host, scene.Map, maps, scene.Player);
        ApplyBooks(host.Player, scene.Hud);
        host.Player.PoseVitals(new Vitals(scene.Hud.Health, scene.Hud.MaxHealth, scene.Hud.Mana, scene.Hud.MaxMana));
        var hud = scene.Hud;
        host.Player.PoseProgression(new CharacterProgression(
            hud.Level,
            hud.AbilityLevel,
            hud.Experience,
            hud.ToNextLevel,
            hud.AbilityExperience,
            hud.ToNextAbility,
            hud.GamePoints,
            hud.Gold));
        ApplyHud(host.Hud, scene.Hud, scene.Player);
        ApplyNotices(host, scene.Hud);
        ApplyWindow(host, scene.Hud.Window, scene.Hud.Name, scene.Player);
        ApplyNpc(host, scene.Hud.Npc, scene.Hud.Gold);
        ApplyObjects(host, scene);
        host.World.SetHovered(scene.Map.HoverObject is { } hovered ? (hovered < 0 ? PlayerEntity : EntityOf(hovered)) : default);
    }

    public static (int Width, int Height) MapSize(SceneMap map, MapCatalog? maps)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (map.Width > 0 && map.Height > 0)
        {
            return (map.Width, map.Height);
        }

        return maps?.Find(map.Id) is { Width: > 0, Height: > 0 } known ? (known.Width, known.Height) : (0, 0);
    }

    private static void ApplyMap(SceneHost host, SceneMap map, MapCatalog? maps, SceneObject player)
    {
        if (map.Id <= 0)
        {
            host.UnloadMap();
            return;
        }

        var (width, height) = MapSize(map, maps);
        if (host.MapId != map.Id || host.MapWidth != width || host.MapHeight != height
            || host.MapFlags != map.Flags || host.TimeStep != map.TimeStep)
        {
            host.LoadMap(map.Id, width, height, (MapFlags)map.Flags, map.TimeStep);
        }

        host.SetAnimationTicks(map.TileTick, map.StaticTick);
        host.World.Renderer.SetHoverCell(map.HoverX ?? -1, map.HoverY ?? -1);

        host.CenterOn(player.X, player.Y, player.OffsetX, player.OffsetY);

        host.ResetDoors();
        foreach (var door in map.Doors)
        {
            host.SetDoor(door.X, door.Y, door.Left, door.Open);
        }
    }

    private static void ApplyBooks(PlayerModel player, SceneHud values)
    {
        Fill(player.Spells, values.Spells);
        Fill(player.Skills, values.Skills);

        player.Inventory.Clear();
        foreach (var item in values.Inventory)
        {
            player.Inventory.Set(new InventoryItem(
                (byte)item.Slot, ItemSprite(item.Icon), item.Dye, Bytes(item.Name), item.Quantity, item.Quantity > 1, 0, 0));
        }

        player.Equipment.Clear();
        foreach (var item in values.Equipment)
        {
            player.Equipment.Set(new EquippedItem(
                (EquipmentSlot)item.Slot, ItemSprite(item.Icon), item.Dye, Bytes(item.Name), 0, 0));
        }

        static ushort ItemSprite(int icon) => icon > 0 ? (ushort)(IconArt.ItemTag + icon) : (ushort)0;

        static void Fill(SlotBook book, List<SceneSlot> slots)
        {
            book.Clear();
            foreach (var slot in slots)
            {
                book.Set(slot.Slot, (ushort)slot.Icon, Bytes(slot.Name));
                if (slot.Cooldown != CooldownKind.None)
                {
                    book.PoseActionDelay(
                        slot.Slot, true, slot.Cooldown == CooldownKind.Wipe ? slot.CooldownProgress : null);
                }
            }
        }
    }

    private static void ApplyNotices(SceneHost host, SceneHud values)
    {
        host.Notices.Clear();
        foreach (var line in values.Notices)
        {
            host.Notices.Append(Bytes(line.Text + "\n"), line.Color ?? NoticePalette, StageTime);
        }

        if (values.Notices.Count > 0 && values.NoticeFade > 0)
        {
            var now = StageTime + (uint)GameMessagePane.HoldMs;
            for (var step = 0; step < values.NoticeFade; step++)
            {
                host.Notices.Tick(now);
                now += GameMessagePane.FadeIntervalMs;
            }
        }
    }

    public const byte NoticePalette = 0x10;

    private static void ApplyWindow(SceneHost host, SceneWindow window, string name, SceneObject player)
    {
        host.Hud.SetSystemButton(-1);
        host.Options.Close();
        host.CloseProfile();
        host.Users.Dismiss();
        host.CloseBulletin();

        switch (window.Kind)
        {
            case SceneWindowKind.Options:
                host.Hud.SetSystemButton(0);
                host.Options.Tick(StageTime);
                host.Options.Open(window.SoundLevel, window.MusicLevel);
                for (var step = 1; step <= OptionPane.SlideSteps; step++)
                {
                    host.Options.Tick(StageTime + ((uint)step * OptionPane.SlideStepMs));
                }

                break;

            case SceneWindowKind.Profile:
                ApplyProfile(host, window.Profile, name, player);
                break;

            case SceneWindowKind.UserList:
                ApplyUsers(host, window.Users, name);
                break;

            case SceneWindowKind.BoardList or SceneWindowKind.PostList or SceneWindowKind.Post:
                ApplyBulletin(host, window.Kind, window.Bulletin);
                break;
        }
    }

    private static void ApplyNpc(SceneHost host, SceneNpc? npc, uint gold)
    {
        var dialog = host.Npc;
        host.Tooltip.Hide(dialog.Shop);

        if (npc is null)
        {
            dialog.Close();
            return;
        }

        dialog.Show(new NpcMessage(Bytes(npc.Name), Bytes(npc.Text))
        {
            SpeakerSprite = npc.Sprite,
            SpeakerColor = npc.Color,
            Illustration = npc.Illustration,
            IsScreenMenu = !npc.Pursuit,
            HasPrevious = npc.HasPrevious,
            HasNext = npc.HasNext,
        });

        if (npc.Shop is { Open: true } shop)
        {
            var items = shop.Items
                .Select(i => new ShopItem(Bytes(i.Name), i.Icon > 0 ? (ushort)(IconArt.ItemTag + i.Icon) : (ushort)0, i.Color, i.Price))
                .ToArray();

            var list = dialog.Shop;
            list.Show(items, gold);
            list.SelectCategory(shop.Category);
            list.SetPage(shop.Page);
            list.SelectRow(shop.SelectedRow ?? -1);

            if (shop.HoverRow is { } row)
            {
                list.Hover(row, shop.PointerX - list.Bounds.X, shop.PointerY - list.Bounds.Y);
            }

            return;
        }

        if (npc.Choices.Count > 0)
        {
            dialog.Menu.Begin(NpcMenuForm.TextMenu);
            foreach (var choice in npc.Choices)
            {
                dialog.Menu.AddRow(Bytes(choice.Text), default);
            }

            dialog.Menu.EndRows();
            dialog.Menu.HoveredRow = npc.HoverChoice ?? -1;
        }
    }

    private static void ApplyBulletin(SceneHost host, SceneWindowKind kind, SceneBulletin bulletin)
    {
        host.Hud.SetSystemButton(1);
        var session = host.OpenBulletin();
        switch (kind)
        {
            case SceneWindowKind.BoardList:
            {
                var boards = new BoardListDialogPane(host.Ui, host.Blender);
                session.Add(boards);
                boards.Apply([.. bulletin.Boards.Select(b => new BulletinBoard((ushort)b.Id, Bytes(b.Name)))]);
                session.Push(boards);
                boards.StartOpenAnimation(StageTime);
                for (var step = 1; step < BoardListDialogPane.AnimationSteps; step++)
                {
                    boards.Tick(StageTime + ((uint)step * BoardListDialogPane.AnimationStepMilliseconds));
                }

                break;
            }

            case SceneWindowKind.PostList:
            {
                session.SectionId = (ushort)bulletin.SectionId;
                var list = new BulletinListDialogPane(host.Ui, host.Blender, bulletin.Mailbox, bulletin.Privileged);
                session.Add(list);
                var rows = bulletin.Posts
                    .Select(p => new BulletinEntry(p.Flags, (ushort)p.Id, Bytes(p.Author), p.Month, p.Day, Bytes(p.Title)))
                    .ToArray();
                list.Apply(new BulletinListing((ushort)bulletin.SectionId, Bytes(bulletin.Heading), bulletin.Permissions, rows), first: true);
                session.Push(list);
                break;
            }

            case SceneWindowKind.Post:
            {
                var post = bulletin.Post;
                var entry = new BulletinEntryDialogPane(host.Ui, host.Blender, bulletin.Mailbox);
                session.Add(entry);
                entry.Apply(new BulletinArticle(
                    (ushort)post.Id,
                    Bytes(post.Author),
                    Bytes(post.Title),
                    Bytes(post.Body.Replace("\\n", "\n", StringComparison.Ordinal)),
                    post.Month,
                    post.Day,
                    post.NavigationFlags));
                session.Push(entry);
                break;
            }
        }
    }

    private static void ApplyUsers(SceneHost host, SceneUserList users, string name)
    {
        var rows = new List<ShowUsersRow>();
        foreach (var row in users.Rows)
        {
            var flags = (byte)(((byte)row.Class & ShowUsersRow.ClassMask) | (row.Guild ? ShowUsersRow.GuildFlag : 0));
            var entry = new ShowUsersRow(flags, row.Color, row.State, Bytes(row.Title), row.Master ? (byte)1 : (byte)0, Bytes(row.Name));
            for (var i = 0; i < Math.Max(1, row.Repeat); i++)
            {
                rows.Add(entry);
            }
        }

        host.Hud.SetSystemButton(2);
        var window = host.Users;
        window.SetSelfName(Bytes(name));
        window.Tick(StageTime);
        window.Apply((ushort)users.WorldCount, CollectionsMarshal.AsSpan(rows), null, null);
        window.Open();
        if (users.Filter != 0)
        {
            window.Answer(users.Filter + 1);
        }

        for (var step = 1; step <= WindowSlide.Steps; step++)
        {
            window.Tick(StageTime + ((uint)step * WindowSlide.StepMs));
        }
    }

    private static void ApplyProfile(SceneHost host, SceneProfile profile, string name, SceneObject player)
    {
        host.Player.PoseStats(
            new CharacterAttributes(profile.Strength, profile.Intelligence, profile.Wisdom, profile.Constitution, profile.Dexterity),
            profile.ArmorClass);

        var paperdoll = host.Profile;
        paperdoll.SetName(Bytes(name));
        paperdoll.SetUserState(profile.State);
        paperdoll.SetNation(profile.Nation);
        paperdoll.SetNationName(Bytes(profile.NationName));

        paperdoll.SetFigure(new HumanState(ToAppearance(player.Look ?? new AislingLook())));
        paperdoll.SetProfile(Bytes(profile.Title), Bytes(profile.DisplayClass), Bytes(profile.Guild), Bytes(profile.GuildRank));
        paperdoll.SetGroupOpen(profile.GroupOpen);
        paperdoll.SetProfileText(Bytes(profile.Text.Replace("\n", "\r", StringComparison.Ordinal)));
        paperdoll.SetPortrait(profile.Portrait ?? []);

        var marks = profile.Legend
            .Select(m => new LegendMark(m.Icon, m.Color, Bytes(m.Key), Bytes(m.Text)))
            .ToArray();
        host.ProfilePages[UserInfoPagePane.LegendPage - UserInfoPagePane.FirstPage].SetLegendMarks(marks);

        host.OpenProfile(Math.Clamp(profile.Page, 0, UserInfoTabPane.CloseTab - 1));
    }

    private static void ApplyHud(GuiBackPane hud, SceneHud values, SceneObject player)
    {
        hud.SetLayout(values.Layout);
        hud.SetName(Bytes(values.Name));
        hud.SetServer(Bytes(values.Server));
        hud.SetZone(Bytes(values.Zone));
        hud.SetPosition(values.PositionX ?? player.X, values.PositionY ?? player.Y, true);
        hud.SetWeight(values.Weight, values.MaxWeight);
        hud.SetVitals(values.Health, values.MaxHealth, values.Mana, values.MaxMana);
        hud.SetNetworkLatency(values.Latency);

        switch (values.Panel)
        {
            case ScenePanel.Chat:
                hud.ShowChat();
                break;

            case ScenePanel.Inventory:
                hud.ShowItemInventory();
                break;

            case ScenePanel.Sheet:
                hud.ShowSheet();
                break;

            case ScenePanel.SpellsPage1:
                hud.TrayMode = TrayMode.SpellPage1;
                break;

            case ScenePanel.SpellsPage2:
                hud.TrayMode = TrayMode.SpellPage2;
                break;

            case ScenePanel.SkillsPage1:
                hud.TrayMode = TrayMode.SkillPage1;
                break;

            case ScenePanel.SkillsPage2:
                hud.TrayMode = TrayMode.SkillPage2;
                break;

            case ScenePanel.General:
                hud.TrayMode = TrayMode.Combined;
                break;
        }

        var effects = new (short Icon, byte Stage)[values.StatusEffects.Count];
        for (var i = 0; i < effects.Length; i++)
        {
            var effect = values.StatusEffects[i];
            effects[i] = (effect.Icon is { } icon ? (short)icon : SpelledViewPane.EmptySlot, effect.Stage);
        }

        hud.Spelled?.SetSlots(effects);

        hud.ResetLogs();

        foreach (var line in values.History)
        {
            hud.AppendHistory(Bytes(line));
        }

        foreach (var line in values.Chat)
        {
            if (line.Color is { } color)
            {
                hud.ChatLog.Append(Bytes(line.Text), color);
            }
            else
            {
                hud.ChatLog.Append(Bytes(line.Text));
            }
        }

        if (values.Description.Length > 0)
        {
            hud.SetDescription(Bytes(values.Description));
        }
        else
        {
            hud.ClearDescription(0);
        }
    }

    private static void ApplyObjects(SceneHost host, SceneDocument scene)
    {
        var table = host.Scene.Objects;
        table.Clear();
        host.Balloons.Clear();
        host.Scene.Effects.Clear();

        var player = scene.Player;
        player.Kind = SceneObjectKind.Aisling;
        ApplyObject(host, player, PlayerEntity, isPlayer: true);

        for (var i = 0; i < scene.Objects.Count; i++)
        {
            ApplyObject(host, scene.Objects[i], EntityOf(i), isPlayer: false);
        }

        host.Scene.Effects.Tick(StageTime, host.Scene.ObjectArt);
    }

    private static void ApplyEffects(SceneHost host, SceneObject staged, EntityId entity)
    {
        if (host.Scene.ObjectArt is not { } art)
        {
            return;
        }

        foreach (var effect in staged.Effects)
        {
            var interval = (uint)Math.Max(art.EffectFrameIntervalMs(effect.Id), 1);
            var started = StageTime - (uint)Math.Max(effect.Step, 0) * interval;
            host.Scene.Effects.ShowOn(entity, effect.Id, interval, started, staged.Direction);
        }
    }

    private static void ApplyObject(SceneHost host, SceneObject staged, EntityId entity, bool isPlayer)
    {
        var table = host.Scene.Objects;
        var position = new MapPoint(staged.X, staged.Y);
        var committed = new MapPoint(staged.FromX ?? staged.X, staged.FromY ?? staged.Y);

        switch (staged.Kind)
        {
            case SceneObjectKind.Aisling:
                var state = new HumanState(ToAppearance(staged.Look ?? new AislingLook()));
                var entry = new WorldObjectEntry(
                    entity,
                    position,
                    staged.Direction,
                    state.Appearance.HeadSprite,
                    WorldObjectKind.Human,
                    in state,
                    isPlayer);

                table.Set(in entry, committed);

                if (!isPlayer && staged.Name.Length > 0)
                {
                    table.SetName(entity, Bytes(staged.Name), human: true, 0);
                }

                break;

            case SceneObjectKind.Creature:
                table.Replace(new WorldObjectEntry(
                    entity,
                    position,
                    staged.Direction,
                    staged.Sprite,
                    staged.CreatureType == CreatureType.Mundane ? WorldObjectKind.Mundane : WorldObjectKind.Creature,
                    staged.CreatureType));

                table.SetCreatureColoring(entity, CreatureColoring.From([]));

                if (staged.Name.Length > 0)
                {
                    table.SetName(entity, Bytes(staged.Name), human: false, 0);
                }

                break;

            case SceneObjectKind.Item:
                table.Replace(new WorldObjectEntry(
                    entity,
                    position,
                    staged.Direction,
                    staged.Sprite,
                    WorldObjectKind.Item,
                    CreatureType.Monster,
                    staged.Color));
                break;
        }

        if (staged.Kind != SceneObjectKind.Item)
        {
            var walk = WalkState.Posed(
                staged.OffsetX,
                staged.OffsetY,
                staged.WalkFrame,
                staged.Kind == SceneObjectKind.Creature && staged.WalkFrame > 0 ? staged.WalkFrame : -1);

            var standing = staged.Kind == SceneObjectKind.Creature
                && staged.IdleSegment is { } segment
                && host.TryGetStandingSegment(staged.Sprite, segment, out var picked)
                    ? CreatureStanding.Posed(in picked, Math.Clamp(staged.IdleStep, 0, Math.Max(0, picked.Steps - 1)))
                    : default;

            table.SetPose(
                entity, in walk, MotionState.Posed(staged.Animation, staged.AnimationFrame, staged.IdleStep), in standing);

            if (isPlayer)
            {
                table.SetLocalCrossing(
                    staged.OffsetX,
                    staged.OffsetY,
                    staged.WalkFrame);
            }
        }

        ApplyEffects(host, staged, entity);

        if (staged.Balloon is { Text.Length: > 0 } balloon)
        {
            host.Balloons.Show(entity, balloon.Mode, Bytes(BalloonText(staged.Name, balloon)), StageTime);
        }
    }

    public static string BalloonText(string speaker, SceneBalloon balloon)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(balloon);

        if (speaker.Length == 0)
        {
            return balloon.Text;
        }

        return balloon.Mode switch
        {
            SpeechMode.Say => $"{speaker}: {balloon.Text}",
            SpeechMode.Shout => $"{speaker}! {balloon.Text}",
            _ => balloon.Text,
        };
    }

    public static readonly EntityId PlayerEntity = new(1);

    public static EntityId EntityOf(int index) => new((uint)index + 2);

    public static int? IndexOf(EntityId entity) => entity.Value switch
    {
        0 => null,
        1 => -1,
        var value => (int)value - 2,
    };

    public static HumanAppearance ToAppearance(AislingLook look)
    {
        ArgumentNullException.ThrowIfNull(look);

        var body = HumanBody.FromPacked((byte)((look.Form << 4) | (look.Pants & 0x0F)));
        return new HumanAppearance
        {
            Body = body,
            HeadSprite = look.Head,
            HairColor = look.HairColor,
            FaceShape = look.Face,
            SkinColor = look.Skin,
            ArmorSprite = look.Armor,
            ArmsSprite = look.Arms,
            ArmorColor = look.ArmorColor,
            OvercoatSprite = look.Overcoat,
            OvercoatColor = look.OvercoatColor,
            BootsSprite = look.Boots,
            BootsColor = look.BootsColor,
            WeaponSprite = look.Weapon,
            ShieldSprite = look.Shield,
            Accessory1Sprite = look.Accessory1,
            Accessory1Color = look.Accessory1Color,
            Accessory2Sprite = look.Accessory2,
            Accessory2Color = look.Accessory2Color,
            Accessory3Sprite = look.Accessory3,
            Accessory3Color = look.Accessory3Color,
            IsTranslucent = look.Translucent || body.ForcesTranslucent,
        };
    }

    private static byte[] Bytes(string text) => TextEncoding.GetBytes(text);
}
