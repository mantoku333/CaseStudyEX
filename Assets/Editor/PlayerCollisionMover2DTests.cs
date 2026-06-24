using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Gameplay")]
public sealed class PlayerCollisionMover2DTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void CalculateSlideDelta_WhenMovingIntoVerticalWall_StopsBeforeWall()
    {
        PlayerCollisionMover2D mover = CreatePlayer(Vector2.zero, out _, out _);
        CreateGroundBox("Wall", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 appliedDelta = mover.CalculateSlideDelta(new Vector2(3f, 0f));

        Assert.That(appliedDelta.x, Is.GreaterThan(0.9f));
        Assert.That(appliedDelta.x, Is.LessThanOrEqualTo(0.98f));
        Assert.That(Mathf.Abs(appliedDelta.y), Is.LessThan(0.001f));
    }

    [Test]
    public void CalculateSlideDelta_WhenMovingDiagonallyIntoWall_SlidesAlongWall()
    {
        PlayerCollisionMover2D mover = CreatePlayer(Vector2.zero, out _, out _);
        CreateGroundBox("Wall", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 appliedDelta = mover.CalculateSlideDelta(new Vector2(3f, 1f));

        Assert.That(appliedDelta.x, Is.GreaterThan(0.9f));
        Assert.That(appliedDelta.x, Is.LessThanOrEqualTo(0.98f));
        Assert.That(appliedDelta.y, Is.GreaterThan(0.9f));
    }

    [Test]
    public void CalculateSlideDelta_WhenMovingIntoCShape_DoesNotEndOverlapped()
    {
        PlayerCollisionMover2D mover = CreatePlayer(Vector2.zero, out Rigidbody2D rigidbody2D, out Collider2D playerCollider);
        CreateGroundBox("RightWall", new Vector2(2f, 0f), new Vector2(0.2f, 2.4f));
        CreateGroundBox("TopWall", new Vector2(1f, 1f), new Vector2(2.2f, 0.2f));
        CreateGroundBox("BottomWall", new Vector2(1f, -1f), new Vector2(2.2f, 0.2f));
        Physics2D.SyncTransforms();

        Vector2 appliedDelta = mover.CalculateSlideDelta(new Vector2(3f, 0.7f));
        rigidbody2D.position += appliedDelta;
        Physics2D.SyncTransforms();

        Assert.That(CountGroundOverlaps(playerCollider), Is.EqualTo(0));
    }

    [Test]
    public void ProjectVelocityForNextFixedStep_WhenRecoilingIntoWall_RemovesWallVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.98f, 0f), out _, out _);
        CreateGroundBox("Wall", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectVelocityForNextFixedStep(new Vector2(10f, 5f));

        Assert.That(Mathf.Abs(projectedVelocity.x), Is.LessThan(0.01f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(4.9f));
    }

    [Test]
    public void ProjectVelocityForNextFixedStep_WhenRepeatedAgainstThickWall_DoesNotMoveRigidbody()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out Rigidbody2D rigidbody2D, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        Vector2 startPosition = rigidbody2D.position;
        Vector2 velocity = new Vector2(10f, 5f);

        for (int i = 0; i < 8; i++)
        {
            velocity = mover.ProjectVelocityForNextFixedStep(velocity);
            Assert.That(rigidbody2D.position.x, Is.EqualTo(startPosition.x).Within(0.0001f));
            Assert.That(rigidbody2D.position.y, Is.EqualTo(startPosition.y).Within(0.0001f));
        }

        Assert.That(Mathf.Abs(velocity.x), Is.LessThan(0.01f));
        Assert.That(velocity.y, Is.GreaterThan(4.9f));
    }

    [Test]
    public void ProjectVectorAwayFromSolidContacts_WhenTouchingThickRightWall_RemovesOnlyIntoWallVector()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out _, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVector = mover.ProjectVectorAwayFromSolidContacts(new Vector2(3f, 2f));

        Assert.That(Mathf.Abs(projectedVector.x), Is.LessThan(0.01f));
        Assert.That(projectedVector.y, Is.GreaterThan(1.9f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenNoWallInPath_PreservesVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(Vector2.zero, out _, out _);
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(5f);

        Assert.That(projectedVelocity, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenStandingOnFlatGround_PreservesVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0f, 0.5f), out _, out _);
        CreateGroundBox("Floor", new Vector2(0f, -0.5f), new Vector2(10f, 1f));
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(5f);

        Assert.That(projectedVelocity, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenFallingNearFlatGround_PreservesVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0f, 1.53f), out _, out _, new Vector2(1.35f, 2.94f));
        CreateGroundBox("Floor", new Vector2(0f, -0.5f), new Vector2(10f, 1f));
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(5f);

        Assert.That(projectedVelocity, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenMovingIntoThickWall_RemovesHorizontalVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out _, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(10f);

        Assert.That(Mathf.Abs(projectedVelocity), Is.LessThan(0.01f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenMovingAwayFromThickWall_PreservesVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out _, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(-5f);

        Assert.That(projectedVelocity, Is.EqualTo(-5f).Within(0.001f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenMovingIntoCShapeOpenSide_TreatsShapeAsFlatWall()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(3f, 0f), out _, out _);
        CreateGroundBox("CShapeBackWall", new Vector2(1f, 0f), new Vector2(0.2f, 3f));
        CreateGroundBox("CShapeTopLip", new Vector2(2f, 1f), new Vector2(2.2f, 0.2f));
        CreateGroundBox("CShapeBottomLip", new Vector2(2f, -1f), new Vector2(2.2f, 0.2f));
        Physics2D.SyncTransforms();

        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(-100f);

        Assert.That(Mathf.Abs(projectedVelocity), Is.LessThan(0.01f));
    }

    [Test]
    public void ProjectHorizontalVelocityForNextFixedStep_WhenCShapeOpeningTooShort_BlocksWithoutMovingRigidbody()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(3f, 0f), out Rigidbody2D rigidbody2D, out _);
        CreateGroundBox("CShapeTopLip", new Vector2(2f, 0.35f), new Vector2(2.2f, 0.2f));
        CreateGroundBox("CShapeBottomLip", new Vector2(2f, -0.35f), new Vector2(2.2f, 0.2f));
        Physics2D.SyncTransforms();

        Vector2 startPosition = rigidbody2D.position;
        float projectedVelocity = mover.ProjectHorizontalVelocityForNextFixedStep(-100f);

        Assert.That(Mathf.Abs(projectedVelocity), Is.LessThan(0.01f));
        Assert.That(rigidbody2D.position.x, Is.EqualTo(startPosition.x).Within(0.0001f));
        Assert.That(rigidbody2D.position.y, Is.EqualTo(startPosition.y).Within(0.0001f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingRightWallAndRecoilingAway_PreservesLiftAndAwayVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(1f, 0f), out _, out _);
        CreateGroundBox("Wall", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(-10f, 10f));

        Assert.That(projectedVelocity.x, Is.LessThan(-9.9f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingRightWallAndRecoilingIntoWall_RemovesOnlyIntoWallVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(1f, 0f), out _, out _);
        CreateGroundBox("Wall", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(10f, 10f));

        Assert.That(Mathf.Abs(projectedVelocity.x), Is.LessThan(0.01f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingThickRightWallAndRecoilingIntoWall_RemovesOnlyIntoWallVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out _, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(10f, 10f));

        Assert.That(Mathf.Abs(projectedVelocity.x), Is.LessThan(0.01f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingLeftWallAndRecoilingIntoWall_RemovesOnlyIntoWallVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(-1f, 0f), out _, out _);
        CreateGroundBox("Wall", new Vector2(-2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(-10f, 10f));

        Assert.That(Mathf.Abs(projectedVelocity.x), Is.LessThan(0.01f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingThickLeftWallAndRecoilingIntoWall_RemovesOnlyIntoWallVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(-0.5f, 0f), out _, out _);
        CreateGroundBox("ThickWall", new Vector2(-3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(-10f, 10f));

        Assert.That(Mathf.Abs(projectedVelocity.x), Is.LessThan(0.01f));
        Assert.That(projectedVelocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenRepeatedAgainstThickWall_DoesNotMoveRigidbody()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0.5f, 0f), out Rigidbody2D rigidbody2D, out _);
        CreateGroundBox("ThickWall", new Vector2(3f, 0f), new Vector2(4f, 5f));
        Physics2D.SyncTransforms();

        Vector2 startPosition = rigidbody2D.position;
        Vector2 velocity = new Vector2(10f, 10f);

        for (int i = 0; i < 8; i++)
        {
            velocity = mover.ProjectRecoilVelocityForNextFixedStep(velocity);
            Assert.That(rigidbody2D.position.x, Is.EqualTo(startPosition.x).Within(0.0001f));
            Assert.That(rigidbody2D.position.y, Is.EqualTo(startPosition.y).Within(0.0001f));
        }

        Assert.That(Mathf.Abs(velocity.x), Is.LessThan(0.01f));
        Assert.That(velocity.y, Is.GreaterThan(9.9f));
    }

    [Test]
    public void ProjectRecoilVelocityForNextFixedStep_WhenTouchingCeiling_RemovesUpwardVelocity()
    {
        PlayerCollisionMover2D mover = CreatePlayer(new Vector2(0f, 1f), out _, out _);
        CreateGroundBox("Ceiling", new Vector2(0f, 2f), new Vector2(5f, 1f));
        Physics2D.SyncTransforms();

        Vector2 projectedVelocity = mover.ProjectRecoilVelocityForNextFixedStep(new Vector2(0f, 10f));

        Assert.That(Mathf.Abs(projectedVelocity.y), Is.LessThan(0.01f));
    }

    [Test]
    public void CalculateSlideDelta_WhenOnlyFallThroughFloorInPath_DoesNotBlock()
    {
        PlayerCollisionMover2D mover = CreatePlayer(Vector2.zero, out _, out _);
        CreateFallThroughBox("FallThroughFloor", new Vector2(2f, 0f), new Vector2(1f, 5f));
        Physics2D.SyncTransforms();

        Vector2 desiredDelta = new Vector2(3f, 0f);
        Vector2 appliedDelta = mover.CalculateSlideDelta(desiredDelta);

        Assert.That(appliedDelta.x, Is.EqualTo(desiredDelta.x).Within(0.001f));
        Assert.That(appliedDelta.y, Is.EqualTo(desiredDelta.y).Within(0.001f));
    }

    private PlayerCollisionMover2D CreatePlayer(
        Vector2 position,
        out Rigidbody2D rigidbody2D,
        out Collider2D collider2D)
    {
        return CreatePlayer(position, out rigidbody2D, out collider2D, Vector2.one);
    }

    private PlayerCollisionMover2D CreatePlayer(
        Vector2 position,
        out Rigidbody2D rigidbody2D,
        out Collider2D collider2D,
        Vector2 colliderSize)
    {
        GameObject playerObject = CreateObject("Player", position);
        rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.gravityScale = 0f;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D capsule = playerObject.AddComponent<CapsuleCollider2D>();
        capsule.size = colliderSize;
        capsule.direction = CapsuleDirection2D.Vertical;
        collider2D = capsule;

        PlayerCollisionMover2D mover = playerObject.AddComponent<PlayerCollisionMover2D>();
        mover.SetSolidLayerMask(GroundMask());
        mover.SetSkinWidth(0.03f);
        return mover;
    }

    private GameObject CreateGroundBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateObject(name, position);
        box.layer = GroundLayer();
        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return box;
    }

    private GameObject CreateFallThroughBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateObject(name, position);
        box.layer = FallThroughFloorLayer();
        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return box;
    }

    private int CountGroundOverlaps(Collider2D playerCollider)
    {
        Collider2D[] overlaps = new Collider2D[8];
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        filter.SetLayerMask(GroundMask());
        return playerCollider.Overlap(filter, overlaps);
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static LayerMask GroundMask()
    {
        return 1 << GroundLayer();
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for player collision mover tests.");
        return groundLayer;
    }

    private static int FallThroughFloorLayer()
    {
        int layer = LayerMask.NameToLayer("FallThroughFloor");
        Assert.GreaterOrEqual(layer, 0, "FallThroughFloor layer must exist for player collision mover tests.");
        return layer;
    }
}
