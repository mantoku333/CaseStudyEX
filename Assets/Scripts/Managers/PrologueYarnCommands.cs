using System.Collections;
using Yarn.Unity;

public static class PrologueYarnCommands
{
    [YarnCommand("pg_black")]
    public static IEnumerator PgBlack(float alpha, float durationSeconds)
    {
        yield return ProloguePresentationRuntime.Instance.FadeBlack(alpha, durationSeconds);
    }

    [YarnCommand("scene_black")]
    public static IEnumerator SceneBlack(float alpha, float durationSeconds)
    {
        yield return PgBlack(alpha, durationSeconds);
    }

    [YarnCommand("pg_stage")]
    public static void PgStage(string stageId)
    {
        ProloguePresentationRuntime.Instance.SetStage(stageId);
    }

    [YarnCommand("scene_stage")]
    public static void SceneStage(string stageId)
    {
        PgStage(stageId);
    }

    [YarnCommand("pg_sfx")]
    public static void PgSfx(string clipKey)
    {
        ProloguePresentationRuntime.Instance.PlaySfx(clipKey);
    }

    [YarnCommand("scene_sfx")]
    public static void SceneSfx(string clipKey)
    {
        PgSfx(clipKey);
    }

    [YarnCommand("pg_rain_start")]
    public static void PgRainStart(string clipKey, float volume)
    {
        ProloguePresentationRuntime.Instance.StartRain(clipKey, volume);
    }

    [YarnCommand("scene_rain_start")]
    public static void SceneRainStart(string clipKey, float volume)
    {
        PgRainStart(clipKey, volume);
    }

    [YarnCommand("pg_rain_stop")]
    public static IEnumerator PgRainStop(float fadeSeconds)
    {
        yield return ProloguePresentationRuntime.Instance.StopRain(fadeSeconds);
    }

    [YarnCommand("scene_rain_stop")]
    public static IEnumerator SceneRainStop(float fadeSeconds)
    {
        yield return PgRainStop(fadeSeconds);
    }

    [YarnCommand("pg_actor_active")]
    public static void PgActorActive(string actorId, float active)
    {
        ProloguePresentationRuntime.Instance.SetActorActive(actorId, active >= 0.5f);
    }

    [YarnCommand("scene_actor_active")]
    public static void SceneActorActive(string actorId, float active)
    {
        PgActorActive(actorId, active);
    }

    [YarnCommand("pg_actor_place")]
    public static void PgActorPlace(string actorId, string markerId)
    {
        ProloguePresentationRuntime.Instance.PlaceActor(actorId, markerId);
    }

    [YarnCommand("scene_actor_place")]
    public static void SceneActorPlace(string actorId, string markerId)
    {
        PgActorPlace(actorId, markerId);
    }

    [YarnCommand("pg_actor_move")]
    public static IEnumerator PgActorMove(string actorId, string markerId, float durationSeconds)
    {
        yield return ProloguePresentationRuntime.Instance.MoveActor(actorId, markerId, durationSeconds);
    }

    [YarnCommand("scene_actor_move")]
    public static IEnumerator SceneActorMove(string actorId, string markerId, float durationSeconds)
    {
        yield return PgActorMove(actorId, markerId, durationSeconds);
    }

    [YarnCommand("pg_actor_face")]
    public static void PgActorFace(string actorId, string direction)
    {
        ProloguePresentationRuntime.Instance.FaceActor(actorId, direction);
    }

    [YarnCommand("scene_actor_face")]
    public static void SceneActorFace(string actorId, string direction)
    {
        PgActorFace(actorId, direction);
    }

    [YarnCommand("pg_wait")]
    public static IEnumerator PgWait(float seconds)
    {
        yield return ProloguePresentationRuntime.Instance.WaitRealtime(seconds);
    }

    [YarnCommand("scene_wait")]
    public static IEnumerator SceneWait(float seconds)
    {
        yield return PgWait(seconds);
    }
}
