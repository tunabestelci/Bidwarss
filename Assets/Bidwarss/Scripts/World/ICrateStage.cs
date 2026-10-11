using UnityEngine;

namespace Bidwarss
{
    // A crate that stands inside a container: the wooden crate (screws come out one by one) or the taped cardboard box
    // (the tape is cut with the box cutter). WarehouseWorld talks to it through this interface only, so the world code
    // does not depend on the crate animation packages.
    // Opening is "hold E" work that the server accumulates in CrateState.progress (0..1). Letting go of E keeps the
    // progress: the next press continues exactly where it stopped. The items never fly out: they stay inside the
    // crate and the players take them out themselves.
    public interface ICrateStage
    {
        // False while the opening is still running (the items inside must not be shown yet).
        bool ItemsReleased { get; }
        // Seconds of holding E needed for the whole opening (all screws / the whole tape).
        float WorkSeconds { get; }
        // The free space inside the crate, in world space: centre of the inner floor, the long horizontal axis of the
        // inside, inner length (along that axis), width and height. False if the crate cannot tell.
        bool Interior(out Vector3 floorCenter, out Vector3 longAxis, out float length, out float width, out float height);
        // Server, at round start: the crate takes the shape of what is inside. cells = the space each item takes
        // (CratePacking.Cell: x along the long side, y up, z across). Returns the model variant and the shape
        // (stored in CrateState.variant / .shape so every client builds the same crate).
        int ChooseShape(Vector3[] cells, int seed, out Vector3 shape);
        // Where the tool is working right now (first-person hand animation): the point on the crate, the direction the
        // work moves along and the outward surface normal. False if the crate has no such point.
        bool WorkPoint(out Vector3 point, out Vector3 along, out Vector3 normal);
        // How many separate strokes the opening takes (wooden crate: one per screw; taped box: 1). Drives the tool animation.
        int WorkSteps { get; }
    }
}
