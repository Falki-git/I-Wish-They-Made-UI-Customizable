using System;
using UnityEngine;
using CustomizableUI.Utilities;
using ILogger = ReduxLib.Logging.ILogger;

namespace CustomizableUI.Groups
{
    /// <summary>
    /// Runtime wrapper around a single top-level flight-HUD group. Replaces the legacy
    /// BaseGroup/TopLevelGroup pair. Screen-edge/center anchor points are derived purely from
    /// the resolved RectTransform's own pivot/rect/lossyScale rather than hand-tuned per-group
    /// pixel offsets -- a live diagnostic dump showed pivots varying wildly per group
    /// ((0.5,0), (1,0), (0,0), (1,1), (0.5,1), ...), so a single hardcoded fudge factor per
    /// group could never generalize. This also makes the math automatically correct for any
    /// resolution/aspect ratio and resilient to future Redux HUD layout changes, since it just
    /// reads whatever's actually there at runtime instead of trusting a stale constant.
    /// </summary>
    public class GroupHandle
    {
        private static readonly ILogger Logger = ReduxLib.ReduxLib.GetLogger($"CustomizableUI|{nameof(GroupHandle)}");

        /// <summary>Runtime GameObject name -- the stable identity used for save/load and the catalog lookup.</summary>
        public readonly string Key;
        public readonly string DisplayName;

        /// <summary>The top-level instantiated group object; toggled active/inactive to show/hide the group.</summary>
        public readonly Transform GroupRoot;

        /// <summary>The transform actually moved around -- GroupRoot itself, or a descendant that carries the RectTransform.</summary>
        public readonly Transform Positionable;

        public RectTransform RectTransform => Positionable as RectTransform;

        public readonly Vector3 DefaultPosition;

        /// <summary>See GroupCatalog.GetDefaultAttachToNavball -- a handful of groups default to following the navball.</summary>
        public readonly bool DefaultAttachToNavball;

        /// <summary>Manual, per-group correction applied only to the overlay's drawn position -- see OverlayCorrections.</summary>
        private readonly Vector2 _overlayCorrection;

        public bool AttachToNavball;

        public Vector3 Position
        {
            get => Positionable.position;
            set
            {
                var old = Positionable.position;
                Positionable.position = value;

                // Temporary instrumentation: three rounds of guessing at what's writing an
                // unexpected position on group selection have all been wrong, so log the actual
                // call stack for every real position change instead of guessing again.
                if (old != value)
                    Logger.LogInfo($"[PosWrite] {Key}: {old} -> {value}\n{new System.Diagnostics.StackTrace(1, false)}");
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                GroupRoot.gameObject.SetActive(value);
            }
        }

        public GroupHandle(string key, Transform groupRoot)
        {
            Key = key;
            GroupRoot = groupRoot;
            Positionable = groupRoot.ResolvePositionable();
            DisplayName = GroupCatalog.GetDisplayName(key);
            _overlayCorrection = OverlayCorrections.Get(key);

            DefaultPosition = Positionable.position;
            DefaultAttachToNavball = GroupCatalog.GetDefaultAttachToNavball(key);
            AttachToNavball = DefaultAttachToNavball;
            _isActive = true;
            IsActive = true;
        }

        public override string ToString() => DisplayName ?? Key ?? string.Empty;

        // ---- Live widget geometry, in screen pixels ----

        /// <summary>On-screen width, derived from the RectTransform's own local size and world scale.</summary>
        public float WidthPx => RectTransform != null ? RectTransform.rect.width * RectTransform.lossyScale.x : 0f;

        /// <summary>On-screen height, derived from the RectTransform's own local size and world scale.</summary>
        public float HeightPx => RectTransform != null ? RectTransform.rect.height * RectTransform.lossyScale.y : 0f;

        private Vector2 Pivot => RectTransform != null ? RectTransform.pivot : new Vector2(0.5f, 0.5f);

        public float LeftEdge => Position.x - WidthPx * Pivot.x + _overlayCorrection.x;
        public float TopEdge => Position.y + HeightPx * (1f - Pivot.y) + _overlayCorrection.y;

        // ---- Anchor math: the pivot-space X/Y needed to put an edge/center at a screen edge/center ----

        public float HorizontalLeftFar => Round(WidthPx * Pivot.x);
        public float HorizontalLeftMiddle => Round((HorizontalLeftFar + HorizontalCenter) / 2f);
        public float HorizontalCenter => Round(Screen.width / 2f - WidthPx * (0.5f - Pivot.x));
        public float HorizontalRightMiddle => Round((HorizontalCenter + HorizontalRightFar) / 2f);
        public float HorizontalRightFar => Round(Screen.width - WidthPx * (1f - Pivot.x));

        public float VerticalUpperTop => Round(Screen.height - HeightPx * (1f - Pivot.y));
        public float VerticalUpperMiddle => Round((VerticalCenter + VerticalUpperTop) / 2f);
        public float VerticalCenter => Round(Screen.height / 2f - HeightPx * (0.5f - Pivot.y));
        public float VerticalLowerMiddle => Round((VerticalLowerBottom + VerticalCenter) / 2f);
        public float VerticalLowerBottom => Round(HeightPx * Pivot.y);

        private static float Round(float value) => (float)Math.Round(value, 0);

        // ---- Move-to-anchor ----

        public void MoveToHorizontalLeftFar() => Position = new Vector3(HorizontalLeftFar, Position.y, Position.z);
        public void MoveToHorizontalLeftMiddle() => Position = new Vector3(HorizontalLeftMiddle, Position.y, Position.z);
        public void MoveToHorizontalCenter() => Position = new Vector3(HorizontalCenter, Position.y, Position.z);
        public void MoveToHorizontalRightMiddle() => Position = new Vector3(HorizontalRightMiddle, Position.y, Position.z);
        public void MoveToHorizontalRightFar() => Position = new Vector3(HorizontalRightFar, Position.y, Position.z);

        public void MoveToVerticalUpperTop() => Position = new Vector3(Position.x, VerticalUpperTop, Position.z);
        public void MoveToVerticalUpperMiddle() => Position = new Vector3(Position.x, VerticalUpperMiddle, Position.z);
        public void MoveToVerticalCenter() => Position = new Vector3(Position.x, VerticalCenter, Position.z);
        public void MoveToVerticalLowerMiddle() => Position = new Vector3(Position.x, VerticalLowerMiddle, Position.z);
        public void MoveToVerticalLowerBottom() => Position = new Vector3(Position.x, VerticalLowerBottom, Position.z);

        // ---- Nudge (1px steps, meant to be called repeatedly while a button is held) ----

        public void NudgeLeft() => Position += new Vector3(-1f, 0f, 0f);
        public void NudgeRight() => Position += new Vector3(1f, 0f, 0f);
        public void NudgeUp() => Position += new Vector3(0f, 1f, 0f);
        public void NudgeDown() => Position += new Vector3(0f, -1f, 0f);

        /// <summary>
        /// Re-pushes our own IsActive intent onto the GameObject without touching state.
        /// KSP.UI.Flight.UIFlightHud owns these same instrument GameObjects and periodically
        /// force-SetActives all of them back on (SetVesselInstrumentDisplay/EnableVesselInstruments,
        /// on VesselChanged/VesselCreated messages) regardless of what this mod set -- there's no
        /// reliable message-order hook to beat that, so the mod's Update loop calls this every
        /// frame to win the fight instead of applying the hide once and hoping it sticks.
        /// </summary>
        public void EnforceVisibility()
        {
            if (GroupRoot.gameObject.activeSelf != _isActive)
                GroupRoot.gameObject.SetActive(_isActive);
        }

        public void ResetToDefault()
        {
            Position = DefaultPosition;
            IsActive = true;
            AttachToNavball = DefaultAttachToNavball;
        }

        public GroupLayout ToLayout()
        {
            var pos = Position;
            return new GroupLayout
            {
                Key = Key,
                PositionX = pos.x,
                PositionY = pos.y,
                PositionZ = pos.z,
                IsActive = IsActive,
                AttachToNavball = AttachToNavball,
            };
        }

        public void ApplyLayout(GroupLayout layout)
        {
            Position = new Vector3(layout.PositionX, layout.PositionY, layout.PositionZ);
            IsActive = layout.IsActive;
            AttachToNavball = layout.AttachToNavball;
        }
    }
}
