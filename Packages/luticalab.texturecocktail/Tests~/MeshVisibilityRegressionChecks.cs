using System;
using System.Collections.Generic;
using System.Reflection;
using LuticaLab.TextureCocktail;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>CPU geometry and native mesh-capture regressions, copied to Assets/Editor by the test runner.</summary>
public static class MeshVisibilityRegressionChecks
{
    public static int Run()
    {
        int passed = 0;
        Check("directional occlusion, visible neighbors and target-only results", TestOcclusion, ref passed);
        Check("BVH internal nodes preserve planar visibility results", TestHierarchy, ref passed);
        Check("distant directional bounds preserve millimeter occlusion gaps", TestLongRayOcclusion, ref passed);
        Check("front winding and two-sided targets/blockers", TestFacing, ref passed);
        Check("camera frustum and near/far clipping", TestCameraClipping, ref passed);
        Check("orthographic clip volumes can start behind the camera", TestSignedOrthographicClip, ref passed);
        Check("self-occlusion excludes only the sampled triangle", TestSelfOcclusion, ref passed);
        Check("thin triangles and degenerate geometry", TestThinAndDegenerate, ref passed);
        Check("cancellation and invalid view/sample inputs", TestCancellation, ref passed);
        Check("spherical direction coverage includes nested XYZ and diagonal rays", TestDirectionCoverage, ref passed);
        Check("all-direction candidates are sealed triangles and internal shells", TestAllDirectionsHidden, ref passed);
        Check("any visible direction preserves exposed cube and back-side faces", TestAllDirectionsExposure, ref passed);
        Check("vertical rays preserve a face visible only through the top", TestAllDirectionsTopOpening, ref passed);
        Check("diagonal rays reveal a face missed by all six axis views", TestAllDirectionsDiagonalOpening, ref passed);
        Check("increasing direction density cannot introduce hidden candidates", TestAllDirectionsDensity, ref passed);
        Check("multi-view cancellation preserves geometry and monotonic progress", TestAllDirectionsCancellation, ref passed);
        Check("multi-view guards and single-view report compatibility", TestAllDirectionsGuards, ref passed);
        Check("static capture transforms, blockers and preview exclusions", TestStaticCapture, ref passed);
        Check("current skinned pose applies renderer scale once", TestSkinnedCapture, ref passed);
        Check("Read/Write-disabled mesh capture without source changes", TestReadOnlyCapture, ref passed);
        Check("nontriangle submesh and missing mesh warnings", TestCaptureWarnings, ref passed);
        Check("candidate overlays own only temporary meshes/materials", TestOverlay, ref passed);
        return passed;
    }

    private static void TestOcclusion()
    {
        var hidden = Triangle(new Vector3(0, 0, 0), 0.5f);
        var visible = Triangle(new Vector3(3, 0, 0), 0.5f);
        var cover = Triangle(new Vector3(0, 0, -1), 2, false);
        var surfaces = new[] { hidden, visible, cover };
        var view = MeshVisibilityView.FromDirection(new Bounds(new Vector3(1, 0, 0), Vector3.one * 10), Vector3.forward);
        foreach (int samples in new[] { 1, 4, 7 })
        {
            var report = MeshVisibilityAnalyzer.Analyze(surfaces, view, false, samples);
            Require(report.Surfaces.Count == 2, "Blockers must participate without appearing as selected results.");
            Require(State(report, hidden) == MeshVisibilityState.Occluded, "A fully covered triangle must be occluded.");
            Require(State(report, visible) == MeshVisibilityState.Visible, "A triangle beside the cover must remain visible.");
            Require(report.SamplesTested >= 2, "The report must count evaluated samples.");
        }
    }

    private static void TestHierarchy()
    {
        var surfaces = new List<MeshVisibilitySurface>();
        for (int i = 0; i < 20; i++) surfaces.Add(Triangle(new Vector3(i * 2, 0, 0), 0.4f));
        surfaces.Add(new MeshVisibilitySurface(null, new[]
        {
            new Vector3(-1, -2, -1), new Vector3(-1, 2, -1),
            new Vector3(19, 2, -1), new Vector3(19, -2, -1)
        }, new[] { 0, 1, 2, 0, 2, 3 }, false));
        var view = MeshVisibilityView.FromDirection(new Bounds(new Vector3(19, 0, 0), new Vector3(45, 8, 8)), Vector3.forward);
        foreach (int samples in new[] { 1, 4, 7 })
        {
            var report = MeshVisibilityAnalyzer.Analyze(surfaces, view, false, samples);
            Require(report.Surfaces.Count == 20, "The BVH fixture must retain every target result.");
            for (int i = 0; i < 20; i++)
                Require(State(report, surfaces[i]) == (i < 10 ? MeshVisibilityState.Occluded : MeshVisibilityState.Visible),
                    "BVH sorting must not change which side of the planar blocker contains target " + i + ".");
        }
    }

    private static void TestLongRayOcclusion()
    {
        var target = Triangle(Vector3.zero, 0.5f);
        var closeCover = Triangle(new Vector3(0, 0, -0.005f), 2f, false);
        // This unrelated scene mesh extends the directional near plane by ten kilometers.
        var distant = Triangle(new Vector3(100, 0, -10000), 1f, false);
        var surfaces = new[] { target, closeCover, distant };
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var surface in surfaces)
            foreach (Vector3 vertex in surface.Vertices) bounds.Encapsulate(vertex);
        var view = MeshVisibilityView.FromDirection(bounds, Vector3.forward);
        foreach (int samples in new[] { 1, 4, 7 })
        {
            Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, view, false, samples), target)
                == MeshVisibilityState.Occluded,
                "A 5mm blocker gap must remain detectable when unrelated geometry makes a ray over 10km long.");
            Require(State(MeshVisibilityAnalyzer.Analyze(new[] { target, distant }, view, false, samples), target)
                == MeshVisibilityState.Visible, "An unrelated distant object must not occlude the target.");
        }
        closeCover.Triangles = new[] { 0, 2, 1 };
        Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, view, false, 7), target) == MeshVisibilityState.Visible,
            "Reversed query rays must retain original viewer-facing culling for blockers.");
        Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, view, true, 7), target) == MeshVisibilityState.Occluded,
            "Reversed query rays must still include both blocker sides when requested.");
    }

    private static void TestFacing()
    {
        var front = Triangle(Vector3.zero, 1);
        var back = Triangle(Vector3.zero, 1);
        back.Triangles = new[] { 0, 2, 1 };
        var view = MeshVisibilityView.FromDirection(new Bounds(Vector3.zero, Vector3.one * 4), Vector3.forward);
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { front }, view, false, 7), front) == MeshVisibilityState.Visible,
            "Clockwise triangles viewed from -Z must face +Z viewing rays.");
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { back }, view, false, 7), back) == MeshVisibilityState.BackFacing,
            "Reversing winding must identify a back face.");
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { back }, view, true, 7), back) == MeshVisibilityState.Visible,
            "Two-sided mode must accept back-facing targets.");
        var cover = Triangle(new Vector3(0, 0, -1), 2, false);
        cover.Triangles = new[] { 0, 2, 1 };
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { front, cover }, view, false, 7), front) == MeshVisibilityState.Visible,
            "Single-sided analysis must ignore the reverse side of a blocker.");
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { front, cover }, view, true, 7), front) == MeshVisibilityState.Occluded,
            "Two-sided analysis must also use reversed blocker faces.");
    }

    private static void TestCameraClipping()
    {
        using (var fixture = new SceneFixture())
        {
            Camera camera = fixture.NewObject("Visibility camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            camera.nearClipPlane = 1;
            camera.farClipPlane = 10;
            camera.fieldOfView = 60;
            camera.aspect = 1;
            camera.orthographic = false;
            var inside = Triangle(new Vector3(0, 0, 5), 0.5f);
            var side = Triangle(new Vector3(100, 0, 5), 0.5f);
            var beyondFar = Triangle(new Vector3(0, 0, 12), 0.5f);
            var beforeNear = Triangle(new Vector3(0, 0, 0.5f), 2, false);
            var farBlocker = Triangle(new Vector3(0, 0, 14), 2, false);
            var report = MeshVisibilityAnalyzer.Analyze(new[] { inside, side, beyondFar, beforeNear, farBlocker },
                MeshVisibilityView.FromCamera(camera), false, 7);
            Require(State(report, inside) == MeshVisibilityState.Visible, "Blockers before the near plane must not hide in-frustum targets.");
            Require(State(report, side) == MeshVisibilityState.OutsideView, "A triangle outside the side planes must be outside view.");
            Require(State(report, beyondFar) == MeshVisibilityState.OutsideView, "A triangle beyond the far plane must be outside view.");
            var clippedCover = Triangle(new Vector3(0, 0, 2), 2, false);
            report = MeshVisibilityAnalyzer.Analyze(new[] { inside, clippedCover }, MeshVisibilityView.FromCamera(camera), false, 7);
            Require(State(report, inside) == MeshVisibilityState.Occluded, "A blocker between near plane and target must occlude.");
            var onNearPlane = Triangle(new Vector3(0, 0, 1), 2, false);
            report = MeshVisibilityAnalyzer.Analyze(new[] { inside, onNearPlane }, MeshVisibilityView.FromCamera(camera), false, 7);
            Require(State(report, inside) == MeshVisibilityState.Occluded, "A perspective blocker exactly on the near plane must occlude.");

            camera.orthographic = true;
            camera.orthographicSize = 3;
            camera.nearClipPlane = 0.1f;
            var orthoTarget = Triangle(new Vector3(0, 0, 1), 0.5f);
            var orthoBoundary = Triangle(new Vector3(0, 0, 0.1f), 2, false);
            report = MeshVisibilityAnalyzer.Analyze(new[] { orthoTarget, orthoBoundary }, MeshVisibilityView.FromCamera(camera), false, 7);
            Require(State(report, orthoTarget) == MeshVisibilityState.Occluded, "An orthographic blocker exactly on the near plane must occlude.");
            var orthoBeforeNear = Triangle(new Vector3(0, 0, 0.05f), 2, false);
            report = MeshVisibilityAnalyzer.Analyze(new[] { orthoTarget, orthoBeforeNear }, MeshVisibilityView.FromCamera(camera), false, 7);
            Require(State(report, orthoTarget) == MeshVisibilityState.Visible, "An orthographic blocker wholly before near clip must remain excluded.");
        }
    }

    private static void TestSignedOrthographicClip()
    {
        var target = Triangle(new Vector3(0, 0, -1), 0.4f);
        var view = new MeshVisibilityView
        {
            Position = Vector3.zero, Forward = Vector3.forward, Orthographic = true, NearClip = -2,
            WorldToClip = Matrix4x4.Ortho(-2, 2, -2, 2, -2, 10)
                * Matrix4x4.Scale(new Vector3(1, 1, -1))
        };
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { target }, view, false, 7), target)
            == MeshVisibilityState.Visible, "An orthographic target inside a signed clip volume must remain visible.");
        var blocker = Triangle(new Vector3(0, 0, -1.5f), 1, false);
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { target, blocker }, view, false, 7), target)
            == MeshVisibilityState.Occluded, "Signed near-plane rays must include blockers behind the camera position.");
    }

    private static void TestSelfOcclusion()
    {
        var front = Triangle(new Vector3(0, 0, -1), 2);
        var rear = Triangle(Vector3.zero, 0.5f);
        var vertices = new Vector3[6];
        Array.Copy(front.Vertices, 0, vertices, 0, 3);
        Array.Copy(rear.Vertices, 0, vertices, 3, 3);
        var folded = new MeshVisibilitySurface(null, vertices, new[] { 0, 1, 2, 3, 4, 5 });
        var view = MeshVisibilityView.FromDirection(new Bounds(Vector3.zero, Vector3.one * 6), Vector3.forward);
        var report = MeshVisibilityAnalyzer.Analyze(new[] { folded }, view, false, 7);
        Require(report.Surfaces[0].States[0] == MeshVisibilityState.Visible, "A triangle must not occlude its own samples.");
        Require(report.Surfaces[0].States[1] == MeshVisibilityState.Occluded, "Other triangles in the same mesh must still occlude.");
        var coplanar = Triangle(Vector3.zero, 1);
        var duplicate = Triangle(Vector3.zero, 1);
        report = MeshVisibilityAnalyzer.Analyze(new[] { coplanar, duplicate }, view, true, 7);
        Require(State(report, coplanar) == MeshVisibilityState.Visible && State(report, duplicate) == MeshVisibilityState.Visible,
            "Coincident samples must not be falsely blocked by floating-point self-intersections.");
    }

    private static void TestThinAndDegenerate()
    {
        var thin = new MeshVisibilitySurface(null, new[]
        {
            new Vector3(-1, 0, 0), new Vector3(0, 0.0001f, 0), new Vector3(1, 0, 0)
        }, new[] { 0, 1, 2 });
        var degenerate = new MeshVisibilitySurface(null, new[] { Vector3.zero, Vector3.right, Vector3.right * 2 }, new[] { 0, 1, 2 });
        var invalid = new MeshVisibilitySurface(null, new[] { Vector3.zero }, new[] { 0, 1, 2 });
        var view = MeshVisibilityView.FromDirection(new Bounds(Vector3.zero, Vector3.one * 4), Vector3.forward);
        var report = MeshVisibilityAnalyzer.Analyze(new[] { thin, degenerate, invalid }, view, false, 7);
        Require(State(report, thin) == MeshVisibilityState.Visible, "A thin but non-collinear triangle must remain analyzable.");
        Require(State(report, degenerate) == MeshVisibilityState.Degenerate, "Collinear vertices must be reported as degenerate.");
        Require(State(report, invalid) == MeshVisibilityState.Degenerate, "Invalid triangle indices must not crash analysis.");
    }

    private static void TestCancellation()
    {
        var surface = Triangle(Vector3.zero, 1);
        Vector3[] original = (Vector3[])surface.Vertices.Clone();
        var view = MeshVisibilityView.FromDirection(new Bounds(Vector3.zero, Vector3.one * 4), Vector3.forward);
        bool canceled = false;
        try { MeshVisibilityAnalyzer.Analyze(new[] { surface }, view, false, 4, progress => true); }
        catch (OperationCanceledException) { canceled = true; }
        Require(canceled, "Cancellation must stop analysis through OperationCanceledException.");
        for (int i = 0; i < original.Length; i++) Require(surface.Vertices[i] == original[i], "Cancellation must not mutate the input geometry.");
        bool badDirection = false;
        try { MeshVisibilityView.FromDirection(new Bounds(Vector3.zero, Vector3.one), Vector3.zero); }
        catch (ArgumentException) { badDirection = true; }
        Require(badDirection, "A zero viewing direction must be rejected.");
        bool badSamples = false;
        try { MeshVisibilityAnalyzer.Analyze(new[] { surface }, view, false, 2); }
        catch (ArgumentOutOfRangeException) { badSamples = true; }
        Require(badSamples, "Unsupported sample counts must be rejected explicitly.");
        float lastProgress = -1;
        MeshVisibilityAnalyzer.Analyze(new[] { surface }, view, false, 4, progress =>
        {
            Require(progress >= 0 && progress <= 1, "Progress must remain within [0,1].");
            lastProgress = progress;
            return false;
        });
        Require(lastProgress == 1, "Successful analysis must report completion.");
    }

    private static void TestDirectionCoverage()
    {
        IReadOnlyList<Vector3> previous = null;
        int[] resolutions = { 1, 2, 4 };
        int[] expectedCounts = { 26, 98, 386 };
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        for (int density = 0; density < resolutions.Length; density++)
        {
            var directions = MeshVisibilityDirections.Create(resolutions[density]);
            Require(directions.Count == expectedCounts[density], "The spherical cube grid must retain its documented direction count.");
            foreach (Vector3 axis in axes)
                Require(ContainsDirection(directions, axis), "Every density must inspect both signs of all three axes: " + axis);
            Require(ContainsDirection(directions, Vector3.one.normalized), "The coarsest coverage must already include corner directions.");
            for (int i = 0; i < directions.Count; i++)
            {
                Vector3 direction = directions[i];
                Require(!float.IsNaN(direction.x) && !float.IsNaN(direction.y) && !float.IsNaN(direction.z)
                    && !float.IsInfinity(direction.x) && !float.IsInfinity(direction.y) && !float.IsInfinity(direction.z),
                    "Generated viewing directions must be finite.");
                Require(Mathf.Abs(direction.sqrMagnitude - 1f) < 0.00001f, "Viewing directions must be normalized.");
                Require(ContainsDirection(directions, -direction), "Each ray must have its opposite viewing direction.");
                for (int j = 0; j < i; j++)
                    Require((direction - directions[j]).sqrMagnitude > 0.00000001f, "The direction grid must not duplicate views.");
            }
            if (previous != null)
                foreach (Vector3 direction in previous)
                    Require(ContainsDirection(directions, direction), "A denser grid must keep every previously checked direction.");
            previous = directions;
        }
    }

    private static void TestAllDirectionsHidden()
    {
        var internalFace = Triangle(Vector3.zero, 0.1f);
        var internalShell = Cube(Vector3.zero, 0.5f);
        var cover = Cube(Vector3.zero, 2f, false);
        var bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
        foreach (int samples in new[] { 1, 4, 7 })
        {
            var report = MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { internalFace, internalShell, cover }, bounds, false, samples, 1);
            Require(report.IsMultiView && report.DirectionsTested == 26, "The report must identify every completed direction.");
            Require(report.CheckedDirections.Count == 26 && report.DirectionSummaries.Count == 26,
                "The audit report must retain one summary for each checked direction.");
            Require(report.Surfaces.Count == 2, "An unselected shell must occlude without becoming a target result.");
            RequireAllStates(report, internalFace, MeshVisibilityState.HiddenFromAllDirections,
                "A fully enclosed face must be hidden from every checked direction.");
            RequireAllStates(report, internalShell, MeshVisibilityState.HiddenFromAllDirections,
                "Every triangle of a fully enclosed shell must be a persistent hidden candidate.");
            for (int i = 0; i < report.DirectionSummaries.Count; i++)
            {
                RequireNear(report.DirectionSummaries[i].Forward, report.CheckedDirections[i], "Direction summary order");
                Require(report.DirectionSummaries[i].VisibleTriangles == 0,
                    "External blockers must not count as visible selected triangles in direction summaries.");
            }
            Require(report.SamplesTested > 0, "Multi-view reports must count the rays evaluated for enclosed targets.");
        }
    }

    private static void TestAllDirectionsExposure()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.one * 8f);
        var exposedCube = Cube(Vector3.zero, 1f);
        foreach (bool twoSided in new[] { false, true })
        {
            var report = MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { exposedCube }, bounds, twoSided, 7, 1);
            RequireAllStates(report, exposedCube, MeshVisibilityState.Visible,
                "Every outward face of an exposed cube must be visible from at least one direction.");
        }
        var face = Triangle(Vector3.zero, 0.2f);
        var frontCover = Triangle(Vector3.back, 2f, false);
        var frontView = MeshVisibilityView.FromDirection(bounds, Vector3.forward);
        Require(State(MeshVisibilityAnalyzer.Analyze(new[] { face, frontCover }, frontView, true, 7), face)
            == MeshVisibilityState.Occluded, "The back-side fixture must first be hidden in its front view.");
        var allViews = MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { face, frontCover }, bounds, true, 7, 1);
        Require(State(allViews, face) == MeshVisibilityState.Visible,
            "A face visible from behind must not stay hidden merely because the front view is occluded.");
        Require(State(MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { face }, bounds, false, 7, 1), face)
            == MeshVisibilityState.Visible, "An exposed single-sided face must survive its other back-facing views.");
        var degenerate = new MeshVisibilitySurface(null, new[] { Vector3.zero, Vector3.right, Vector3.right * 2 }, new[] { 0, 1, 2 });
        var invalid = new MeshVisibilitySurface(null, new[] { Vector3.zero }, new[] { 0, 1, 2 });
        var reportWithInvalid = MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { degenerate, invalid }, bounds, false, 7, 1);
        Require(State(reportWithInvalid, degenerate) == MeshVisibilityState.Degenerate
            && State(reportWithInvalid, invalid) == MeshVisibilityState.Degenerate,
            "Invalid or zero-area geometry must never be promoted to hidden candidates.");
    }

    private static void TestAllDirectionsTopOpening()
    {
        // Four walls and a floor hide the target from horizontal/bottom rays; only the open roof exposes it.
        var walls = Cube(Vector3.zero, 2f, false);
        var withoutRoof = new List<int>();
        for (int i = 0; i < walls.Triangles.Length; i += 3)
        {
            bool onRoof = true;
            for (int j = 0; j < 3; j++) onRoof &= walls.Vertices[walls.Triangles[i + j]].y > 0;
            if (!onRoof) for (int j = 0; j < 3; j++) withoutRoof.Add(walls.Triangles[i + j]);
        }
        walls.Triangles = withoutRoof.ToArray();
        var target = OrientedTriangle(Vector3.zero, 0.15f, Vector3.up);
        var bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
        var surfaces = new[] { target, walls };
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.forward, Vector3.back })
            Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, MeshVisibilityView.FromDirection(bounds, direction), true, 7), target)
                != MeshVisibilityState.Visible, "The roof fixture must not expose the interior from any of its five closed axes.");
        Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, MeshVisibilityView.FromDirection(bounds, Vector3.down), true, 7), target)
            == MeshVisibilityState.Visible, "A ray from above must see the interior through the open roof.");
        var report = MeshVisibilityAnalyzer.AnalyzeAllDirections(surfaces, bounds, true, 7, 1);
        Require(State(report, target) == MeshVisibilityState.Visible, "Full coverage must include elevation, not only horizontal yaw.");
        bool foundTop = false;
        foreach (var summary in report.DirectionSummaries)
            if ((summary.Forward - Vector3.down).sqrMagnitude < 0.00000001f)
            {
                foundTop = true;
                Require(summary.VisibleTriangles == 1, "The -Y audit row must show the face revealed from above.");
            }
        Require(foundTop, "The report must expose its -Y light inspection explicitly.");
    }

    private static void TestAllDirectionsDiagonalOpening()
    {
        // Six small square shutters block the axial sight lines but leave their corner gaps open.
        // An axis-only pass would incorrectly mark this face as permanently hidden.
        var target = OrientedTriangle(Vector3.zero, 0.1f, -Vector3.one.normalized);
        var surfaces = new List<MeshVisibilitySurface> { target };
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 axis in axes) surfaces.Add(Square(axis * 2f, 0.65f, axis));
        var bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
        foreach (Vector3 axis in axes)
            Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, MeshVisibilityView.FromDirection(bounds, axis), true, 7), target)
                == MeshVisibilityState.Occluded, "Each axial shutter must hide every target sample along its axis.");
        Vector3 diagonal = Vector3.one.normalized;
        Require(State(MeshVisibilityAnalyzer.Analyze(surfaces, MeshVisibilityView.FromDirection(bounds, diagonal), true, 7), target)
            == MeshVisibilityState.Visible, "The corner-gap fixture must have an actual diagonal line of sight.");
        var report = MeshVisibilityAnalyzer.AnalyzeAllDirections(surfaces, bounds, true, 7, 1);
        Require(State(report, target) == MeshVisibilityState.Visible,
            "A triangle revealed by a diagonal must not be classified as hidden from all directions.");
        bool foundDiagonal = false;
        foreach (var summary in report.DirectionSummaries)
            if ((summary.Forward - diagonal).sqrMagnitude < 0.00000001f)
            {
                foundDiagonal = true;
                Require(summary.VisibleTriangles == 1, "The diagonal audit row must include its revealed target face.");
            }
        Require(foundDiagonal, "Checked-direction summaries must include the diagonal that disproves occlusion.");
    }

    private static void TestAllDirectionsDensity()
    {
        var enclosed = Triangle(Vector3.zero, 0.1f);
        var enclosingBlocker = Cube(Vector3.zero, 2f, false);
        var exposed = Triangle(Vector3.right * 6f, 0.2f);
        var frontHidden = Triangle(Vector3.right * 12f, 0.2f);
        var frontBlocker = Triangle(Vector3.right * 12f + Vector3.back, 2f, false);
        var surfaces = new[] { enclosed, enclosingBlocker, exposed, frontHidden, frontBlocker };
        var bounds = new Bounds(Vector3.right * 6f, Vector3.one * 30f);
        MeshVisibilityReport previous = null;
        foreach (int resolution in new[] { 1, 2, 4 })
        {
            var report = MeshVisibilityAnalyzer.AnalyzeAllDirections(surfaces, bounds, true, 7, resolution);
            Require(State(report, enclosed) == MeshVisibilityState.HiddenFromAllDirections,
                "Enclosed geometry must remain hidden when more directions are inspected.");
            Require(State(report, exposed) == MeshVisibilityState.Visible && State(report, frontHidden) == MeshVisibilityState.Visible,
                "Increasing inspection density must preserve any earlier evidence of visibility.");
            if (previous != null)
                foreach (var result in report.Surfaces)
                    if (result.States[0] == MeshVisibilityState.HiddenFromAllDirections)
                        Require(State(previous, result.Surface) == MeshVisibilityState.HiddenFromAllDirections,
                            "A denser nested view set may remove hidden candidates, but must never create them.");
            previous = report;
        }
    }

    private static void TestAllDirectionsCancellation()
    {
        var target = Cube(Vector3.zero, 0.5f);
        var blocker = Cube(Vector3.zero, 2f, false);
        var surfaces = new[] { target, blocker };
        var originalVertices = new Vector3[surfaces.Length][];
        var originalTriangles = new int[surfaces.Length][];
        for (int i = 0; i < surfaces.Length; i++)
        {
            originalVertices[i] = (Vector3[])surfaces[i].Vertices.Clone();
            originalTriangles[i] = (int[])surfaces[i].Triangles.Clone();
        }
        var bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
        MeshVisibilityReport partial = null;
        bool canceled = false;
        float lastProgress = -1;
        int updates = 0;
        try
        {
            partial = MeshVisibilityAnalyzer.AnalyzeAllDirections(surfaces, bounds, false, 7, 2, progress =>
            {
                Require(progress >= lastProgress && progress >= 0 && progress <= 1,
                    "Multi-view progress must stay monotonic across preparation and direction boundaries.");
                lastProgress = progress;
                updates++;
                return progress >= 0.72f;
            });
        }
        catch (OperationCanceledException) { canceled = true; }
        Require(canceled && partial == null && updates > 1 && lastProgress >= 0.72f,
            "Cancellation partway through multiple views must throw without publishing a partial candidate report.");
        for (int i = 0; i < surfaces.Length; i++)
        {
            for (int j = 0; j < originalVertices[i].Length; j++)
                Require(originalVertices[i][j] == surfaces[i].Vertices[j], "Canceled inspection must preserve target and blocker vertices.");
            for (int j = 0; j < originalTriangles[i].Length; j++)
                Require(originalTriangles[i][j] == surfaces[i].Triangles[j], "Canceled inspection must preserve target and blocker triangle indices.");
        }
        Require(target.IsTarget && !blocker.IsTarget, "Canceled inspection must preserve target membership.");
        lastProgress = -1;
        MeshVisibilityAnalyzer.AnalyzeAllDirections(surfaces, bounds, false, 4, 1, progress =>
        {
            Require(progress >= lastProgress && progress >= 0 && progress <= 1,
                "Successful inspection must report monotonic progress over the entire view set.");
            lastProgress = progress;
            return false;
        });
        Require(lastProgress == 1, "Successful all-direction analysis must report final completion.");
    }

    private static void TestAllDirectionsGuards()
    {
        var surface = Triangle(Vector3.zero, 1f);
        var bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        foreach (int resolution in new[] { -1, 0, 3, int.MaxValue })
        {
            RequireThrows<ArgumentOutOfRangeException>(() => MeshVisibilityDirections.Create(resolution),
                "Unsupported direction densities must be rejected before allocating a potentially enormous grid.");
            RequireThrows<ArgumentOutOfRangeException>(() => MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { surface }, bounds, false, 7, resolution),
                "The analyzer must reject unsupported inspection densities.");
        }
        RequireThrows<ArgumentOutOfRangeException>(() => MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { surface }, bounds, false, 2, 1),
            "Multi-view analysis must retain the supported sample-count contract.");
        RequireThrows<ArgumentException>(() => MeshVisibilityAnalyzer.AnalyzeAllDirections(new[] { surface },
            new Bounds(new Vector3(float.NaN, 0, 0), Vector3.one), false, 7, 1),
            "Nonfinite directional bounds must be rejected explicitly.");
        var single = MeshVisibilityAnalyzer.Analyze(new[] { surface }, MeshVisibilityView.FromDirection(bounds, Vector3.forward), false, 7);
        Require(State(single, surface) == MeshVisibilityState.Visible && !single.IsMultiView && single.DirectionsTested == 0
            && single.CheckedDirections.Count == 0 && single.DirectionSummaries.Count == 0,
            "Single-view callers must retain their classification and avoid claiming a spherical inspection.");
        Require((int)MeshVisibilityState.Visible == 0 && (int)MeshVisibilityState.Occluded == 1
            && (int)MeshVisibilityState.BackFacing == 2 && (int)MeshVisibilityState.OutsideView == 3
            && (int)MeshVisibilityState.Degenerate == 4,
            "Adding the all-direction candidate state must preserve existing state ordinals.");
        var legacyReport = new MeshVisibilityReport(single.Surfaces, single.SamplesTested);
        Require(!legacyReport.IsMultiView && legacyReport.CheckedDirections.Count == 0 && legacyReport.DirectionSummaries.Count == 0,
            "The legacy report constructor must remain usable without multi-view metadata.");
    }

    private static void TestStaticCapture()
    {
        using (var fixture = new SceneFixture())
        {
            GameObject root = fixture.NewObject("Visibility target");
            root.transform.SetPositionAndRotation(new Vector3(10, 4, -3), Quaternion.Euler(0, 30, 10));
            root.transform.localScale = new Vector3(2, 3, 4);
            GameObject child = fixture.NewObject("Static child", root.transform);
            child.transform.localPosition = new Vector3(1, 2, 3);
            Mesh mesh = fixture.NewMesh();
            MeshRenderer target = AddStatic(child, mesh);
            Vector3[] originalVertices = mesh.vertices;
            int[] originalTriangles = mesh.triangles;
            Matrix4x4 originalTransform = target.localToWorldMatrix;
            MeshRenderer blocker = AddStatic(fixture.NewObject("External blocker"), mesh);
            blocker.transform.position = new Vector3(-30, 0, -40);
            MeshRenderer disabled = AddStatic(fixture.NewObject("Disabled blocker"), mesh);
            disabled.enabled = false;
            MeshRenderer inactive = AddStatic(fixture.NewObject("Inactive blocker"), mesh);
            inactive.gameObject.SetActive(false);
            MeshRenderer preview = AddStatic(fixture.NewPreviewObject("Preview blocker"), mesh);

            var selectedOnly = MeshVisibilitySnapshot.Capture(root, false);
            Require(selectedOnly.Surfaces.Count == 1 && selectedOnly.Surfaces[0].IsTarget,
                "Target-only capture must include exactly the selected active renderer.");
            var captured = FindSurface(selectedOnly, target);
            for (int i = 0; i < originalVertices.Length; i++)
                RequireNear(captured.Vertices[i], originalTransform.MultiplyPoint3x4(originalVertices[i]), "Static world vertex");
            var all = MeshVisibilitySnapshot.Capture(root, true);
            Require(FindSurface(all, blocker) != null && !FindSurface(all, blocker).IsTarget, "An external active renderer must become a blocker.");
            Require(FindSurface(all, disabled) == null && FindSurface(all, inactive) == null, "Disabled/inactive blockers must be excluded.");
            Require(FindSurface(all, preview) == null, "Preview-scene renderers must be excluded.");
            Require(all.Bounds.Contains(FindSurface(all, blocker).Vertices[0]), "Directional bounds must include camera-side blockers.");
            Require(!all.TargetBounds.Contains(FindSurface(all, blocker).Vertices[0]), "Target bounds must not grow to include external blockers.");
            Require(target.GetComponent<MeshFilter>().sharedMesh == mesh && target.localToWorldMatrix == originalTransform,
                "Capture must not replace the shared mesh or transform.");
            for (int i = 0; i < originalVertices.Length; i++) Require(mesh.vertices[i] == originalVertices[i], "Capture must not modify source vertices.");
            for (int i = 0; i < originalTriangles.Length; i++) Require(mesh.triangles[i] == originalTriangles[i], "Capture must not modify source triangles.");
        }
    }

    private static void TestSkinnedCapture()
    {
        using (var fixture = new SceneFixture())
        {
            GameObject root = fixture.NewObject("Skinned target");
            root.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(0, 25, 0));
            root.transform.localScale = new Vector3(2, 3, 4);
            GameObject child = fixture.NewObject("Skinned child", root.transform);
            child.transform.localPosition = new Vector3(1, 0, 1);
            child.transform.localScale = new Vector3(1.5f, 0.75f, 2);
            GameObject bone = fixture.NewObject("Bone", child.transform);
            Mesh mesh = fixture.NewMesh();
            Vector3[] original = mesh.vertices;
            var weights = new BoneWeight[original.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
            mesh.boneWeights = weights;
            mesh.bindposes = new[] { bone.transform.worldToLocalMatrix * child.transform.localToWorldMatrix };
            SkinnedMeshRenderer renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { bone.transform };
            renderer.rootBone = bone.transform;
            renderer.updateWhenOffscreen = true;
            Vector3 poseOffset = new Vector3(0.25f, 0.5f, -0.1f);
            bone.transform.localPosition = poseOffset;
            int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            var snapshot = MeshVisibilitySnapshot.Capture(root, false);
            var surface = FindSurface(snapshot, renderer);
            Require(surface != null, "A currently posed skinned renderer must be captured.");
            for (int i = 0; i < original.Length; i++)
                RequireNear(surface.Vertices[i], child.transform.TransformPoint(original[i] + poseOffset), "Skinned world vertex with scale applied once");
            Require(Resources.FindObjectsOfTypeAll<Mesh>().Length == meshesBefore, "Temporary baked meshes must be destroyed before Capture returns.");
            Require(renderer.sharedMesh == mesh && bone.transform.localPosition == poseOffset, "Capture must not replace the source mesh or change its pose.");
            for (int i = 0; i < original.Length; i++) Require(mesh.vertices[i] == original[i], "Skin capture must not modify source vertices.");
        }
    }

    private static void TestReadOnlyCapture()
    {
        using (var fixture = new SceneFixture())
        {
            GameObject root = fixture.NewObject("Read-only target");
            Mesh mesh = fixture.NewMesh();
            Vector3[] original = mesh.vertices;
            MeshRenderer renderer = AddStatic(root, mesh);
            mesh.UploadMeshData(true);
            Require(!mesh.isReadable, "The test mesh must have runtime Read/Write disabled.");
            var snapshot = MeshVisibilitySnapshot.Capture(root, false);
            var surface = FindSurface(snapshot, renderer);
            Require(surface != null, "Editor read-only capture must support a mesh with runtime Read/Write disabled.");
            for (int i = 0; i < original.Length; i++) RequireNear(surface.Vertices[i], original[i], "Read-only mesh vertex");
            Require(!mesh.isReadable && renderer.GetComponent<MeshFilter>().sharedMesh == mesh,
                "Capture must not enable Read/Write or replace the source mesh.");
        }
    }

    private static void TestCaptureWarnings()
    {
        using (var fixture = new SceneFixture())
        {
            GameObject root = fixture.NewObject("Mixed topology target");
            Mesh mesh = fixture.NewMesh();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 1);
            MeshRenderer renderer = AddStatic(root, mesh);
            AddStatic(fixture.NewObject("Missing mesh", root.transform), null);
            var snapshot = MeshVisibilitySnapshot.Capture(root, false);
            var surface = FindSurface(snapshot, renderer);
            Require(surface != null && surface.Triangles.Length == 3, "Valid triangle submeshes must survive skipped line topology.");
            Require(snapshot.Warnings.Count >= 2, "Skipped nontriangle topology and missing meshes must produce warnings.");
            GameObject empty = fixture.NewObject("Empty target");
            var emptySnapshot = MeshVisibilitySnapshot.Capture(empty, true);
            Require(emptySnapshot.Surfaces.Count == 0 && emptySnapshot.Warnings.Count > 0,
                "An empty selection must report a useful warning without collecting unrelated blockers.");
        }
    }

    private static void TestOverlay()
    {
        Shader shader = Shader.Find("Hidden/TextureCocktail/MeshVisibilityOverlay");
        Require(shader != null && shader.isSupported, "The real candidate overlay shader must compile on this graphics device.");
        var inputMesh = new Mesh { name = "Visibility overlay source", hideFlags = HideFlags.HideAndDontSave };
        var vertices = new List<Vector3>();
        for (int i = 0; i < 4; i++) vertices.AddRange(Triangle(new Vector3(i * 3, 0, 0), 1).Vertices);
        inputMesh.vertices = vertices.ToArray();
        inputMesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        var surface = new MeshVisibilitySurface(null, inputMesh.vertices, inputMesh.triangles);
        var report = new MeshVisibilityReport(new[]
        {
            new MeshVisibilitySurfaceResult(surface, new[]
            {
                MeshVisibilityState.Visible, MeshVisibilityState.Occluded, MeshVisibilityState.OutsideView,
                MeshVisibilityState.HiddenFromAllDirections
            })
        }, 4);
        var overlay = new MeshVisibilityOverlay();
        var ownedMeshes = new List<Mesh>();
        var ownedMaterials = new List<Material>();
        try
        {
            overlay.Build(report);
            Require(overlay.IsAvailable, "A valid report and shader must build an available overlay.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var groups = (System.Collections.IList)typeof(MeshVisibilityOverlay).GetField("_groups", flags).GetValue(overlay);
            var materials = (System.Collections.IDictionary)typeof(MeshVisibilityOverlay).GetField("_materials", flags).GetValue(overlay);
            Require(groups.Count == 3, "Occluded, OutsideView and HiddenFromAllDirections must produce separate candidate groups.");
            foreach (object group in groups)
            {
                Type type = group.GetType();
                var state = (MeshVisibilityState)type.GetField("State", flags).GetValue(group);
                var mesh = (Mesh)type.GetField("Mesh", flags).GetValue(group);
                ownedMeshes.Add(mesh);
                Require(state == MeshVisibilityState.Occluded || state == MeshVisibilityState.OutsideView
                    || state == MeshVisibilityState.HiddenFromAllDirections,
                    "Visible triangles must not enter candidate overlay groups.");
                int offset = state == MeshVisibilityState.Occluded ? 3 : state == MeshVisibilityState.OutsideView ? 6 : 9;
                Require(mesh.triangles.Length == 3 && mesh.triangles[0] == offset
                    && mesh.triangles[1] == offset + 1 && mesh.triangles[2] == offset + 2,
                    "Candidate groups must preserve the original triangle indices.");
            }
            foreach (Material material in materials.Values)
            {
                Require(material.shader == shader, "Overlay materials must use the actual candidate shader.");
                ownedMaterials.Add(material);
            }
            Require(ownedMaterials.Count > 0, "The overlay must own render materials.");
            var hiddenMaterial = materials[MeshVisibilityState.HiddenFromAllDirections] as Material;
            Color hiddenColor = hiddenMaterial != null ? hiddenMaterial.GetColor("_Color") : Color.clear;
            Require(hiddenColor.a > 0 && hiddenColor.r > hiddenColor.g && hiddenColor.b > hiddenColor.g,
                "Persistent hidden candidates must have their own visible purple material.");
            overlay.Dispose();
            Require(!overlay.IsAvailable && groups.Count == 0 && materials.Count == 0, "Dispose must clear overlay state.");
            foreach (Mesh mesh in ownedMeshes) Require(mesh == null, "Dispose must destroy each generated candidate mesh.");
            foreach (Material material in ownedMaterials) Require(material == null, "Dispose must destroy each generated overlay material.");
            Require(inputMesh != null && inputMesh.vertexCount == 12 && inputMesh.triangles.Length == 12,
                "Overlay disposal must leave the source mesh alive and unchanged.");
            for (int i = 0; i < 12; i++) Require(surface.Triangles[i] == i, "Overlay construction must not mutate source index arrays.");
        }
        finally
        {
            overlay.Dispose();
            Object.DestroyImmediate(inputMesh);
        }
    }

    private static MeshVisibilitySurface Triangle(Vector3 center, float size, bool target = true)
        => new MeshVisibilitySurface(null, new[]
        {
            center + new Vector3(-size, -size, 0), center + new Vector3(0, size, 0), center + new Vector3(size, -size, 0)
        }, new[] { 0, 1, 2 }, target);

    private static MeshVisibilitySurface OrientedTriangle(Vector3 center, float size, Vector3 normal)
    {
        var result = Triangle(Vector3.zero, size);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.back, normal);
        for (int i = 0; i < result.Vertices.Length; i++) result.Vertices[i] = center + rotation * result.Vertices[i];
        return result;
    }

    private static MeshVisibilitySurface Square(Vector3 center, float halfSize, Vector3 normal)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
        return new MeshVisibilitySurface(null, new[]
        {
            center + rotation * new Vector3(-halfSize, -halfSize, 0),
            center + rotation * new Vector3(halfSize, -halfSize, 0),
            center + rotation * new Vector3(halfSize, halfSize, 0),
            center + rotation * new Vector3(-halfSize, halfSize, 0)
        }, new[] { 0, 1, 2, 0, 2, 3 }, false);
    }

    private static MeshVisibilitySurface Cube(Vector3 center, float halfSize, bool target = true)
    {
        var corners = new[]
        {
            new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)
        };
        for (int i = 0; i < corners.Length; i++) corners[i] = center + corners[i] * halfSize;
        return new MeshVisibilitySurface(null, corners, new[]
        {
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
            0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5
        }, target);
    }

    private static bool ContainsDirection(IReadOnlyList<Vector3> directions, Vector3 expected)
    {
        foreach (Vector3 direction in directions)
            if ((direction - expected).sqrMagnitude < 0.00000001f) return true;
        return false;
    }

    private static void RequireAllStates(MeshVisibilityReport report, MeshVisibilitySurface surface, MeshVisibilityState expected, string message)
    {
        foreach (var result in report.Surfaces)
        {
            if (!ReferenceEquals(result.Surface, surface)) continue;
            Require(result.States.Length == surface.Triangles.Length / 3, "Every source triangle must have a result state.");
            foreach (var state in result.States) Require(state == expected, message + " Actual state: " + state);
            return;
        }
        throw new InvalidOperationException("Expected surface is missing from the visibility report.");
    }

    private static void RequireThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Mesh visibility regression failed: " + message);
    }

    private static MeshVisibilityState State(MeshVisibilityReport report, MeshVisibilitySurface surface)
    {
        foreach (var result in report.Surfaces) if (ReferenceEquals(result.Surface, surface)) return result.States[0];
        throw new InvalidOperationException("Expected surface is missing from the visibility report.");
    }

    private static MeshVisibilitySurface FindSurface(MeshVisibilitySnapshot snapshot, Renderer renderer)
    {
        foreach (var surface in snapshot.Surfaces) if (surface.Source == renderer) return surface;
        return null;
    }

    private static MeshRenderer AddStatic(GameObject owner, Mesh mesh)
    {
        owner.AddComponent<MeshFilter>().sharedMesh = mesh;
        return owner.AddComponent<MeshRenderer>();
    }

    private static void Check(string name, Action test, ref int passed)
    {
        test();
        passed++;
        Debug.Log("TextureCocktail mesh visibility PASS: " + name);
    }

    private static void RequireNear(Vector3 actual, Vector3 expected, string context)
        => Require((actual - expected).sqrMagnitude < 0.00001f, context + ": expected " + expected + ", got " + actual);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mesh visibility regression failed: " + message);
    }

    private sealed class SceneFixture : IDisposable
    {
        // A batch Editor can start with an unsaved untitled scene, where NewScene(Additive)
        // is rejected. Reuse the active scene and own only the objects created by this fixture.
        private readonly Scene _scene = SceneManager.GetActiveScene();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Scene _preview;

        internal GameObject NewObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            SceneManager.MoveGameObjectToScene(gameObject, _scene);
            if (parent != null) gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        internal GameObject NewPreviewObject(string name)
        {
            if (!_preview.IsValid()) _preview = EditorSceneManager.NewPreviewScene();
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            SceneManager.MoveGameObjectToScene(gameObject, _preview);
            return gameObject;
        }

        internal Mesh NewMesh()
        {
            var mesh = new Mesh { name = "MeshVisibility regression triangle", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(0, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }

        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            if (_preview.IsValid()) EditorSceneManager.ClosePreviewScene(_preview);
            foreach (var mesh in _meshes) if (mesh != null) Object.DestroyImmediate(mesh);
        }
    }
}
