namespace DaScreenshotStudio.Engine.Staging;

public static class PoseMatcher
{
    public const int HumanFrames = 5;

    public const int HumanIdleSteps = 16;

    public const int CreatureWalkSteps = 8;

    public readonly record struct Pose(int WalkFrame, int IdleStep, int? IdleSegment);

    public readonly record struct Result(Pose Pose, int Differing, int Tried);

    public static List<Pose> Candidates(SceneHost host, SceneObject figure)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(figure);

        var poses = new List<Pose> { new(figure.WalkFrame, figure.IdleStep, figure.IdleSegment) };

        switch (figure.Kind)
        {
            case SceneObjectKind.Aisling:
                for (var step = 0; step < HumanIdleSteps; step++)
                {
                    poses.Add(new Pose(0, step, figure.IdleSegment));
                }

                for (var frame = 1; frame < HumanFrames; frame++)
                {
                    poses.Add(new Pose(frame, figure.IdleStep, figure.IdleSegment));
                }

                break;

            case SceneObjectKind.Creature:
                poses.Add(new Pose(0, 0, null));

                var segments = host.StandingSegments(figure.Sprite);
                for (var segment = 0; segment < segments.Length; segment++)
                {
                    for (var step = 0; step < Math.Max(1, segments[segment].Steps); step++)
                    {
                        poses.Add(new Pose(0, step, segment));
                    }
                }

                for (var step = 1; step <= CreatureWalkSteps; step++)
                {
                    poses.Add(new Pose(step, 0, null));
                }

                break;
        }

        return [.. poses.Distinct()];
    }

    public static Result Find(
        SceneHost host,
        SceneDocument scene,
        int index,
        Screenshot reference,
        MapCatalog? maps = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(reference);

        var trial = scene.Clone();
        var figure = index < 0 ? trial.Player : trial.Objects[index];
        var candidates = Candidates(host, figure);

        SceneApplier.Apply(host, trial, maps);
        var region = FigureRegion.Around(host, figure);

        var best = new Result(candidates[0], int.MaxValue, 0);
        foreach (var pose in candidates)
        {
            cancellation.ThrowIfCancellationRequested();

            figure.WalkFrame = pose.WalkFrame;
            figure.IdleStep = pose.IdleStep;
            figure.IdleSegment = pose.IdleSegment;

            var differing = FigureRegion.Score(host, trial, reference, region, maps);

            if (differing < best.Differing)
            {
                best = best with { Pose = pose, Differing = differing };
            }
        }

        return best with { Tried = candidates.Count };
    }
}
