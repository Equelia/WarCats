// RectSpawnArea.cs
using UnityEngine;

public class RectSpawnArea : SpawnAreaBase
{
	[Header("Rectangle")]
	public Vector2 size = new Vector2(6f, 4f);

	protected override Vector3 SampleWorldPoint()
	{
		float x = Random.Range(-size.x * 0.5f, size.x * 0.5f);
		float z = Random.Range(-size.y * 0.5f, size.y * 0.5f);
		Vector3 local = new Vector3(x, 0f, z);
		return transform.TransformPoint(local);
	}

	protected override void DrawGizmosShape()
	{
		// filled thin box on ground
		var box = new Vector3(size.x, 0.05f, size.y);
		Gizmos.DrawCube(Vector3.zero, box);
	}

	protected override void DrawGizmosWire()
	{
		var box = new Vector3(size.x, 0.01f, size.y);
		Gizmos.DrawWireCube(Vector3.zero, box);
	}
}