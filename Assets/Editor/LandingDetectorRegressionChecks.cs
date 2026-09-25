using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic replay of wheel contacts and the delayed grounded flag, without
/// modifying the open scene or PlayerPrefs. Run from Tools or Unity's -executeMethod.</summary>
public static class LandingDetectorRegressionChecks
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/RidingBike/Run Landing Regression Checks")]
    public static void Run()
    {
        DelayedConfirmationDoesNotRepeat();
        ContactBounceDoesNotRearm();
        NextConfirmedFlightStillScores();
        InitialGroundContactDoesNotScore();
        CancelledPendingContactStillLands();
        SingleWheelTimeoutDoesNotRepeat();
        QualityThresholdsStillApply();
        Debug.Log("LANDING_REGRESSION_PASS: 7 checks; one landing + one trick scoring event per confirmed flight.");
    }

    static void DelayedConfirmationDoesNotRepeat()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, true, false, 10f, 10.015f);
            f.Tick(30); // Many rendered frames in the 50ms grounded debounce window.
            f.Expect(1);
            f.Contact(true, true, true);
            f.Tick(30);
            f.Expect(1);
        }
    }

    static void ContactBounceDoesNotRearm()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, true, false);
            f.Tick();
            for (int i = 0; i < 10; i++)
            {
                f.Contact(false, false, false);
                f.Tick();
                f.Contact(true, true, false);
                f.Tick();
            }
            f.Expect(1);
        }
    }

    static void NextConfirmedFlightStillScores()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, true, false);
            f.Tick();
            f.Contact(true, true, true);
            f.Tick();
            f.Contact(false, false, true); // Departure hasn't passed airborne debounce yet.
            f.Tick(10);
            f.Contact(true, true, true); // Short suspension jitter, no new flight.
            f.Tick(10);
            f.Expect(1);
            f.Contact(false, false, false); // Genuine next takeoff.
            f.Tick();
            f.Contact(true, true, false, 20f, 20.015f);
            f.Tick(30);
            f.Expect(2);
        }
    }

    static void InitialGroundContactDoesNotScore()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, true, false);
            f.Landing.Initialize(f.Bike);
            f.Tick(30);
            f.Contact(true, true, true);
            f.Tick(30);
            f.Expect(0);
            f.Contact(false, false, false);
            f.Tick();
            f.Contact(true, true, false);
            f.Tick();
            f.Expect(1);
        }
    }

    static void CancelledPendingContactStillLands()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, false, false);
            f.Tick();
            f.Contact(false, false, false);
            f.Tick();
            f.Expect(0);
            f.Contact(true, true, false);
            f.Tick(30);
            f.Expect(1);
        }
    }

    static void SingleWheelTimeoutDoesNotRepeat()
    {
        using (var f = new Fixture())
        {
            f.Contact(true, false, false, Time.time - 1f, 0f);
            f.Tick(30);
            f.Expect(1);
            Require(f.Landing.LastLanding.Quality == LandingDetector.Quality.NotBad, "Single-wheel timeout quality");
            f.Contact(true, true, true);
            f.Tick(30);
            f.Expect(1);
        }
    }

    static void QualityThresholdsStillApply()
    {
        float[] deltas = { 0.015f, 0.04f, 0.1f };
        var qualities = new[] { LandingDetector.Quality.Perfect, LandingDetector.Quality.Good, LandingDetector.Quality.NotBad };
        for (int i = 0; i < deltas.Length; i++)
        using (var f = new Fixture())
        {
            f.Contact(true, false, false, 10f, 0f);
            f.Tick();
            f.Contact(true, true, false, 10f, 10f + deltas[i]);
            f.Tick(30);
            f.Expect(1);
            Require(f.Landing.LastLanding.Quality == qualities[i], "Landing quality changed");
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    sealed class Fixture : IDisposable
    {
        readonly GameObject host;
        readonly WheelContactSensor front;
        readonly WheelContactSensor back;
        public readonly BikeController Bike;
        public readonly LandingDetector Landing;
        int landings, tricks, landingScores, trickScores;

        public Fixture()
        {
            host = new GameObject("Landing regression fixture");
            host.SetActive(false); // No Awake/physics/gameplay updates; input traces are explicit.
            Bike = host.AddComponent<BikeController>();
            front = host.AddComponent<WheelContactSensor>();
            back = host.AddComponent<WheelContactSensor>();
            Set(Bike, "<FrontWheelContact>k__BackingField", front);
            Set(Bike, "<BackWheelContact>k__BackingField", back);
            Set(Bike, "spinAccumulatedDeg", 605f); // Same one-lap result as the reported trace.
            Landing = host.AddComponent<LandingDetector>();
            Landing.Initialize(Bike);
            var trick = host.AddComponent<TrickSystem>();
            trick.Initialize(Bike, Landing);
            var score = host.AddComponent<ScoreSystem>();
            score.Initialize(Landing, trick, host.AddComponent<ObstacleSpawner>());
            Landing.OnLanded += (_, __) => landings++;
            trick.OnTrickCompleted += _ => tricks++;
            score.OnLandingScored += (_, __) => landingScores++;
            score.OnTrickScored += (_, __) => trickScores++;
        }

        public void Contact(bool frontGrounded, bool backGrounded, bool confirmed, float frontTime = 10f, float backTime = 10.015f)
        {
            Set(front, "groundedLastStep", frontGrounded);
            Set(back, "groundedLastStep", backGrounded);
            Set(front, "<LastGroundedTime>k__BackingField", frontTime);
            Set(back, "<LastGroundedTime>k__BackingField", backTime);
            Set(Bike, "<IsConfirmedGrounded>k__BackingField", confirmed);
        }

        public void Tick(int count = 1)
        {
            MethodInfo update = typeof(LandingDetector).GetMethod("Update", PrivateInstance);
            for (int i = 0; i < count; i++) update.Invoke(Landing, null);
        }

        public void Expect(int expected)
        {
            Require(landings == expected && tricks == expected && landingScores == expected && trickScores == expected,
                $"Expected {expected} landing/trick events; got landing={landings}, trick={tricks}, landingScore={landingScores}, trickScore={trickScores}");
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(host);
        static void Set(object target, string field, object value)
            => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    }
}
