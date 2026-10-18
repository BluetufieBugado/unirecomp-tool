"""Boot test funcional do Tier 0 (espelho de Smoke.Run em Program.cs)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from tier0 import (Application, Bounds, Color, Color32, Component, Debug, GameObject,
                   Mathf, Matrix4x4, MonoBehaviour, Object, Quaternion, Random,
                   Time, Transform, Vector2, Vector3)

PASS = 0


def check(cond, name):
    global PASS
    assert cond, "FALHOU: " + name
    PASS += 1
    print("  ok:", name)


class Enemy(MonoBehaviour):
    def __init__(self, go=None):
        super().__init__(go)
        self.hp = 3
    def TakeDamage(self):
        self.hp -= 1


def main():
    Time.deltaTime = 1 / 60
    # hierarquia
    go = GameObject("player")
    go.transform.localPosition = Vector3(1, 2, 3)
    check(go.transform.position.x == 1, "posicao local")
    child = GameObject("arma")
    child.transform.SetParent(go.transform)
    child.transform.localPosition = Vector3(0, 1, 0)
    check(child.transform.position.y == 3, "mundo via pai")
    check(abs(child.transform.lossyScale.x - 1) < 1e-6, "lossyScale identidade")
    it = [c.gameObject.name for c in go.transform]
    check(it == ["arma"], "enumerador de filhos")
    # matematica
    check(Vector3.Dot(Vector3.right(), Vector3.up()) == 0, "dot ortogonal")
    check(Vector3.Cross(Vector3.right(), Vector3.up()).z == 1, "cross")
    check(Quaternion.identity().eulerAngles.magnitude < 1e-3, "identidade euler~0")
    check(abs(Quaternion.Angle(Quaternion.identity(), Quaternion.Euler(0, 90, 0)) - 90) < 1.0, "angle 90")
    check(Mathf.Clamp(5, 0, 1) == 1 and Mathf.Clamp01(-2) == 0, "clamp")
    check(Mathf.PingPong(3, 2) == 1, "pingpong")
    m = Matrix4x4.TRS(Vector3(1, 0, 0), Quaternion.identity(), Vector3.one())
    check(m.MultiplyPoint3x4(Vector3.zero()).x == 1, "TRS+MultiplyPoint")
    check(Color.Lerp(Color.black(), Color(1, 1, 1), 0.5).r == 0.5, "color lerp")
    check(abs(Color32(255, 0, 0, 255).__implicit__().r - 1.0) < 1e-6, "color32->color")
    check(Bounds(Vector3.zero(), Vector3(2, 2, 2)).max.x == 1, "bounds min/max")
    check(Vector2(3, 4).magnitude == 5, "vector2 magnitude")
    # ciclo de vida
    check(Time.deltaTime > 0, "time via plataforma")
    e = go.AddComponent(Enemy)
    check(go.GetComponent(Enemy) is e, "add+get component")
    check(go.GetComponent("Enemy") is e, "get component generico <T>")
    e.Invoke("TakeDamage", 0)
    check(e.hp == 2, "invoke imediato")
    co = e.StartCoroutine(iter([1]))
    check(co is not None, "startcoroutine")
    child.SendMessageUpwards("TakeDamage")  # sobe: arma(sem)->player(com Enemy)
    check(e.hp == 1, "sendmessage upwards")
    check(GameObject.Find("arma") is child, "find")
    go.tag = "Player"
    check(GameObject.FindGameObjectWithTag("Player") is go, "find tag")
    check(e.CompareTag("Player"), "comparetag")
    check(child.GetComponentInParent(Enemy) is e, "getinparent")
    check(len(go.GetComponentsInChildren(Enemy)) == 1, "getchildren")
    clone = Object.Instantiate(go)
    check(clone is not None and clone != go and clone.name == "player", "instantiate clona")
    Object.Destroy(clone)
    check(not clone, "destroy invalida bool")
    check(Application.platform == "WindowsPlayer", "application plataforma")
    r = Random.Range(0, 10)
    check(0 <= r < 10, "random int")
    check(Random.insideUnitSphere.magnitude <= 1, "insideUnitSphere")
    go.SetActive(False)
    check(not go.activeInHierarchy and not go.activeSelf, "setactive")
    go.transform.LookAt(go.transform.position + Vector3(0, 0, 5))
    check(abs(go.transform.forward.z - 1) < 1e-3, "lookat forward")
    Debug.Log(f"boot Tier 0 ok ({PASS} checks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
