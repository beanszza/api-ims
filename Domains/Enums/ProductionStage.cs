namespace Domains.Enums;

/// <summary>
/// Physical stage a production batch has reached.
/// </summary>
/// <remarks>
/// The stage vocabulary is currently inconsistent across three places, and this enum is the union of
/// all of them so that introducing it breaks nothing:
/// <list type="bullet">
///   <item><c>ViewProduction.tsx</c> STAGES: Preparation, Mixing and Processing, Cooking, Cooling, Quality Control, Packaging.</item>
///   <item><c>UploadImagesModal.tsx</c> stages: Peeling, Steaming, Mixing, Cooking, Cooling, Packaging, QA Review.</item>
///   <item><c>ProductionService</c>: Preparation on create, "QA Review" as a guard, Packaging on QA approval.</item>
/// </list>
/// Note the duplication: Quality Control and QA Review mean the same checkpoint, and
/// Peeling/Steaming/Mixing overlap with Preparation and Mixing and Processing. Task 29 rationalises
/// this into one ordered sequence with a data migration; until then every historical value must
/// still load.
/// </remarks>
public enum ProductionStage
{
    /// <summary>Legacy rows created before a stage was assigned.</summary>
    [DbValue("")]
    Unspecified = 0,

    [DbValue("Preparation")]
    Preparation = 1,

    [DbValue("Peeling")]
    Peeling = 2,

    [DbValue("Steaming")]
    Steaming = 3,

    [DbValue("Mixing")]
    Mixing = 4,

    [DbValue("Mixing and Processing")]
    MixingAndProcessing = 5,

    [DbValue("Cooking")]
    Cooking = 6,

    [DbValue("Cooling")]
    Cooling = 7,

    /// <summary>The in-process QA checkpoint as named by the production board.</summary>
    [DbValue("Quality Control")]
    QualityControl = 8,

    /// <summary>The same checkpoint as named by the upload modal and by ProductionService.</summary>
    [DbValue("QA Review")]
    QaReview = 9,

    [DbValue("Packaging")]
    Packaging = 10,

    [DbValue("Completed")]
    Completed = 11,

    [DbValue("Cancelled")]
    Cancelled = 12
}
