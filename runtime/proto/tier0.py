"""Prototipo executavel do shim Tier 0 (espelho de runtime/host/Shim/).

Serve para validar HOJE o desenho da API contra a checklist
docs/prio_stardust.json, sem .NET SDK. Ver: boot_test.py, check_coverage.py.
"""
import copy
import math
import random as _random


class Debug:
    @staticmethod
    def Log(m): print("[LOG]", m)
    @staticmethod
    def LogWarning(m): print("[WARN]", m)
    @staticmethod
    def LogError(m): print("[ERR]", m)


class Mathf:
    @staticmethod
    def Abs(v): return abs(v)
    @staticmethod
    def Acos(d): return math.acos(d)
    @staticmethod
    def Clamp(v, mn, mx): return mn if v < mn else (mx if v > mx else v)
    @staticmethod
    def Clamp01(v): return Mathf.Clamp(v, 0.0, 1.0)
    @staticmethod
    def Cos(f): return math.cos(f)
    @staticmethod
    def Max(a, b): return a if a > b else b
    @staticmethod
    def PingPong(t, length):
        if length <= 0: return 0
        t = t % (2 * length)
        return length - abs(t - length)
    @staticmethod
    def RoundToInt(f): return int(round(f))
    @staticmethod
    def Sin(f): return math.sin(f)
    @staticmethod
    def SmoothStep(frm, to, t):
        t = Mathf.Clamp01((t - frm) / (to - frm))
        return t * t * (3 - 2 * t)


class _RandomNS:
    """Namespace para Random.Range / Random.insideUnitSphere."""
    def Range(self, a, b):
        if isinstance(a, int) and isinstance(b, int):
            return _random.randrange(a, b)
        return a + _random.random() * (b - a)
    @property
    def insideUnitSphere(self):
        while True:
            v = Vector3(_random.uniform(-1, 1), _random.uniform(-1, 1), _random.uniform(-1, 1))
            if v.magnitude <= 1:
                return v


Random = _RandomNS()


class Time:
    deltaTime = 1 / 60
    fixedDeltaTime = 1 / 50
    realtimeSinceStartup = 0.0
    time = 0.0
    timeScale = 1.0
    unscaledDeltaTime = 1 / 60


class Vector2:
    def __init__(self, x=0.0, y=0.0): self.x, self.y = x, y
    @property
    def magnitude(self): return math.sqrt(self.x**2 + self.y**2)
    @staticmethod
    def one(): return Vector2(1, 1)
    def __sub__(self, o): return Vector2(self.x - o.x, self.y - o.y)
    def __implicit__(self): return Vector3(self.x, self.y, 0)


class Vector3:
    def __init__(self, x=0.0, y=0.0, z=0.0): self.x, self.y, self.z = x, y, z
    @property
    def magnitude(self): return math.sqrt(self.x**2 + self.y**2 + self.z**2)
    @property
    def normalized(self):
        m = self.magnitude
        return Vector3.zero() if m < 1e-6 else self / m
    @staticmethod
    def zero(): return Vector3(0, 0, 0)
    @staticmethod
    def one(): return Vector3(1, 1, 1)
    @staticmethod
    def up(): return Vector3(0, 1, 0)
    @staticmethod
    def right(): return Vector3(1, 0, 0)
    @staticmethod
    def forward(): return Vector3(0, 0, 1)
    @staticmethod
    def Dot(a, b): return a.x*b.x + a.y*b.y + a.z*b.z
    @staticmethod
    def Cross(a, b): return Vector3(a.y*b.z - a.z*b.y, a.z*b.x - a.x*b.z, a.x*b.y - a.y*b.x)
    @staticmethod
    def Lerp(a, b, t):
        t = Mathf.Clamp01(t)
        return Vector3(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t)
    @staticmethod
    def Project(v, n):
        d = Vector3.Dot(n, n)
        return Vector3.zero() if d < 1e-6 else n * (Vector3.Dot(v, n) / d)
    @staticmethod
    def SmoothDamp(cur, target, vel, smooth):
        t = 1 - math.exp(-(1 / smooth if smooth > 0 else 1) * Time.deltaTime)
        r = Vector3.Lerp(cur, target, t)
        vel[0] = (r - cur) / max(Time.deltaTime, 1e-6)
        return r
    def __add__(self, o): return Vector3(self.x+o.x, self.y+o.y, self.z+o.z)
    def __sub__(self, o): return Vector3(self.x-o.x, self.y-o.y, self.z-o.z)
    def __neg__(self): return Vector3(-self.x, -self.y, -self.z)
    def __mul__(self, d): return Vector3(self.x*d, self.y*d, self.z*d)
    __rmul__ = __mul__
    def __truediv__(self, d): return Vector3(self.x/d, self.y/d, self.z/d)
    def __eq__(self, o): return isinstance(o, Vector3) and (self.x, self.y, self.z) == (o.x, o.y, o.z)
    def __repr__(self): return f"V3({self.x},{self.y},{self.z})"


class Vector4:
    def __init__(self, x=0.0, y=0.0, z=0.0, w=0.0): self.x, self.y, self.z, self.w = x, y, z, w


class Quaternion:
    def __init__(self, x=0.0, y=0.0, z=0.0, w=1.0): self.x, self.y, self.z, self.w = x, y, z, w
    @staticmethod
    def identity(): return Quaternion(0, 0, 0, 1)
    @property
    def eulerAngles(self): return Quaternion._to_euler(self)
    @staticmethod
    def Euler(x, y=None, z=None):
        if isinstance(x, Vector3): x, y, z = x.x, x.y, x.z
        return Quaternion._from_euler(x, y, z)
    @staticmethod
    def AngleAxis(deg, axis):
        n = axis.normalized
        h = math.radians(deg) / 2
        s = math.sin(h)
        return Quaternion(n.x*s, n.y*s, n.z*s, math.cos(h))
    @staticmethod
    def LookRotation(fwd, up=None):
        f = fwd.normalized
        up = up or Vector3.up()
        r = Vector3.Cross(up, f).normalized
        u = Vector3.Cross(f, r)
        m00, m01, m02 = r.x, u.x, f.x
        m10, m11, m12 = r.y, u.y, f.y
        m20, m21, m22 = r.z, u.z, f.z
        tr = m00 + m11 + m22
        if tr > 0:
            s = math.sqrt(tr + 1) * 2
            q = Quaternion((m21-m12)/s, (m02-m20)/s, (m10-m01)/s, s/4)
        elif m00 > m11 and m00 > m22:
            s = math.sqrt(1 + m00 - m11 - m22) * 2
            q = Quaternion(s/4, (m01+m10)/s, (m02+m20)/s, (m21-m12)/s)
        elif m11 > m22:
            s = math.sqrt(1 + m11 - m00 - m22) * 2
            q = Quaternion((m01+m10)/s, s/4, (m12+m21)/s, (m02-m20)/s)
        else:
            s = math.sqrt(1 + m22 - m00 - m11) * 2
            q = Quaternion((m02+m20)/s, (m12+m21)/s, s/4, (m10-m01)/s)
        return Quaternion._norm(q)
    @staticmethod
    def Lerp(a, b, t):
        t = Mathf.Clamp01(t)
        return Quaternion._norm(Quaternion(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t, a.z+(b.z-a.z)*t, a.w+(b.w-a.w)*t))
    @staticmethod
    def Slerp(a, b, t): return Quaternion.Lerp(a, b, t)  # TODO arco real
    @staticmethod
    def RotateTowards(frm, to, maxdeg):
        ang = Quaternion.Angle(frm, to)
        if ang <= maxdeg or ang < 1e-4: return to
        return Quaternion.Slerp(frm, to, maxdeg / ang)
    @staticmethod
    def Angle(a, b):
        return math.degrees(math.acos(min(1.0, abs(a.x*b.x + a.y*b.y + a.z*b.z + a.w*b.w)))) * 2
    @staticmethod
    def _norm(q):
        m = math.sqrt(q.x**2 + q.y**2 + q.z**2 + q.w**2)
        return Quaternion.identity() if m < 1e-6 else Quaternion(q.x/m, q.y/m, q.z/m, q.w/m)
    @staticmethod
    def _from_euler(xd, yd, zd):
        cx, sx = math.cos(math.radians(xd)/2), math.sin(math.radians(xd)/2)
        cy, sy = math.cos(math.radians(yd)/2), math.sin(math.radians(yd)/2)
        cz, sz = math.cos(math.radians(zd)/2), math.sin(math.radians(zd)/2)
        return Quaternion(sx*cy*cz + cx*sy*sz, cx*sy*cz - sx*cy*sz,
                          cx*cy*sz - sx*sy*cz, cx*cy*cz + sx*sy*sz)
    @staticmethod
    def _to_euler(q):
        t0 = 2*(q.w*q.x + q.y*q.z); t1 = 1 - 2*(q.x**2 + q.y**2)
        x = math.degrees(math.atan2(t0, t1))
        t2 = max(-1.0, min(1.0, 2*(q.w*q.y - q.z*q.x)))
        y = math.degrees(math.asin(t2))
        t3 = 2*(q.w*q.z + q.x*q.y); t4 = 1 - 2*(q.y**2 + q.z**2)
        z = math.degrees(math.atan2(t3, t4))
        return Vector3(x, y, z)
    def __mul__(self, o):
        if isinstance(o, Vector3):
            p = Quaternion(o.x, o.y, o.z, 0)
            c = Quaternion(-self.x, -self.y, -self.z, self.w)
            r = self * p * c
            return Vector3(r.x, r.y, r.z)
        return Quaternion(
            self.w*o.x + self.x*o.w + self.y*o.z - self.z*o.y,
            self.w*o.y - self.x*o.z + self.y*o.w + self.z*o.x,
            self.w*o.z + self.x*o.y - self.y*o.x + self.z*o.w,
            self.w*o.w - self.x*o.x - self.y*o.y - self.z*o.z)


class Matrix4x4:
    def __init__(self): self.m = [0.0]*16
    @staticmethod
    def TRS(pos, rot, scale):
        r = Matrix4x4()
        x2, y2, z2 = rot.x*2, rot.y*2, rot.z*2
        xx, xy, xz = rot.x*x2, rot.x*y2, rot.x*z2
        yy, yz, zz = rot.y*y2, rot.y*z2, rot.z*z2
        wx, wy, wz = rot.w*x2, rot.w*y2, rot.w*z2
        m = r.m
        m[0]=(1-(yy+zz))*scale.x; m[1]=(xy+wz)*scale.x; m[2]=(xz-wy)*scale.x; m[3]=pos.x
        m[4]=(xy-wz)*scale.y; m[5]=(1-(xx+zz))*scale.y; m[6]=(yz+wx)*scale.y; m[7]=pos.y
        m[8]=(xz+wy)*scale.z; m[9]=(yz-wx)*scale.z; m[10]=(1-(xx+yy))*scale.z; m[11]=pos.z
        m[15]=1.0
        return r
    def SetTRS(self, pos, rot, scale): self.m = Matrix4x4.TRS(pos, rot, scale).m
    def MultiplyPoint3x4(self, p):
        m = self.m
        return Vector3(m[0]*p.x+m[1]*p.y+m[2]*p.z+m[3],
                       m[4]*p.x+m[5]*p.y+m[6]*p.z+m[7],
                       m[8]*p.x+m[9]*p.y+m[10]*p.z+m[11])


class Color:
    def __init__(self, r=0.0, g=0.0, b=0.0, a=1.0): self.r, self.g, self.b, self.a = r, g, b, a
    @staticmethod
    def black(): return Color(0, 0, 0)
    @staticmethod
    def Lerp(a, b, t):
        t = Mathf.Clamp01(t)
        return Color(a.r+(b.r-a.r)*t, a.g+(b.g-a.g)*t, a.b+(b.b-a.b)*t, a.a+(b.a-a.a)*t)


class Color32:
    def __init__(self, r=0, g=0, b=0, a=255): self.r, self.g, self.b, self.a = r, g, b, a
    def __implicit__(self): return Color(self.r/255, self.g/255, self.b/255, self.a/255)


class Bounds:
    def __init__(self, center=None, size=None):
        self.center = center or Vector3.zero()
        self.size = size or Vector3.zero()
    @property
    def min(self): return self.center - self.size / 2
    @property
    def max(self): return self.center + self.size / 2


class Ray:
    def __init__(self, origin=None, direction=None):
        self.origin = origin or Vector3.zero()
        self.direction = direction or Vector3.forward()


class Application:
    platform = "WindowsPlayer"
    version = ""
    targetFrameRate = 60
    @staticmethod
    def Quit(): raise SystemExit(0)


class _Registry:
    roots = []
    all = []


class Object:
    def __init__(self):
        self._destroyed = False
        self.name = ""
        _Registry.all.append(self)
    @staticmethod
    def Destroy(o):
        if o is not None:
            o._destroyed = True
            if o in _Registry.all: _Registry.all.remove(o)
    @staticmethod
    def DontDestroyOnLoad(o): o._keep = True  # Tier 1: sobreviver a troca de cena
    @staticmethod
    def Instantiate(orig):
        if orig is None: return None
        clone = copy.copy(orig)
        if isinstance(clone, GameObject):
            clone._components = []
            for c in orig._components:
                cc = copy.copy(c)
                cc.gameObject = clone
                clone._components.append(cc)
            clone.transform = copy.copy(orig.transform)
            clone.transform.gameObject = clone
            clone.transform._children = []
            clone.transform._parent = None
        clone._destroyed = False
        _Registry.all.append(clone)
        return clone
    def __eq__(self, o):
        if o is None: return getattr(self, "_destroyed", False)
        if getattr(o, "_destroyed", False): return False
        return self is o
    def __ne__(self, o): return not self.__eq__(o)
    def __hash__(self): return id(self)
    def __bool__(self): return not getattr(self, "_destroyed", False)


class Component(Object):
    def __init__(self, game_object=None): super().__init__(); self.gameObject = game_object
    @property
    def transform(self): return self.gameObject.transform if self.gameObject else None
    @property
    def tag(self): return self.gameObject.tag if self.gameObject else ""
    @tag.setter
    def tag(self, v):
        if self.gameObject: self.gameObject.tag = v
    def CompareTag(self, t): return self.gameObject is not None and self.gameObject.tag == t
    def GetComponent(self, t):
        if self.gameObject is None: return None
        if isinstance(t, str):  # variante generica GetComponent<T>
            return self.gameObject.GetComponent(t)
        for c in self.gameObject._components:
            if isinstance(c, t): return c
        return None
    def GetComponentInChildren(self, t):
        hit = self.GetComponent(t)
        if hit: return hit
        if self.gameObject:
            for ch in self.gameObject.transform:
                hit = ch.gameObject.GetComponentInChildren(t)
                if hit: return hit
        return None
    def GetComponentsInChildren(self, t):
        out = []
        def collect(go):
            for c in go._components:
                if isinstance(c, t) if isinstance(t, type) else c.__class__.__name__ == t: out.append(c)
            for ch in go.transform: collect(ch.gameObject)
        if self.gameObject: collect(self.gameObject)
        return out


class Behaviour(Component):
    def __init__(self, game_object=None): super().__init__(game_object); self.enabled = True


class Coroutine(Object): pass


class MonoBehaviour(Behaviour):
    def Invoke(self, method, delay):
        if delay <= 0:
            getattr(self, method)()
        else: self._pending = (method, delay)  # agenda real: Tier 1
    def StartCoroutine(self, routine):
        self._coroutines = getattr(self, "_coroutines", []) + [routine]
        return Coroutine()


class GameObject(Object):
    def __init__(self, name="New Game Object"):
        super().__init__()
        self.name = name
        self.tag = ""
        self._active = True
        self._components = []
        self.transform = Transform(self)
        _Registry.roots.append(self)
    @property
    def gameObject(self): return self
    @property
    def activeSelf(self): return self._active
    @property
    def activeInHierarchy(self):
        p = self.transform.parent
        return self._active and (p is None or p.gameObject.activeInHierarchy)
    def SetActive(self, v): self._active = v
    def AddComponent(self, t):
        c = t(self) if isinstance(t, type) else None
        self._components.append(c)
        return c
    def GetComponent(self, t):
        for c in self._components:
            if isinstance(c, t) if isinstance(t, type) else c.__class__.__name__ == t: return c
        return None
    def GetComponentInChildren(self, t):
        hit = self.GetComponent(t)
        if hit: return hit
        for ch in self.transform:
            hit = ch.gameObject.GetComponentInChildren(t)
            if hit: return hit
        return None
    def GetComponentsInChildren(self, t):
        out = []
        def collect(go):
            for c in go._components:
                if isinstance(c, t) if isinstance(t, type) else c.__class__.__name__ == t: out.append(c)
            for ch in go.transform: collect(ch.gameObject)
        collect(self)
        return out
    def GetComponentInParent(self, t):
        tr = self.transform
        while tr is not None:
            hit = tr.gameObject.GetComponent(t)
            if hit: return hit
            tr = tr.parent
        return None
    @staticmethod
    def Find(n):
        for r in _Registry.roots:
            hit = GameObject._find_in(r, n)
            if hit: return hit
        return None
    @staticmethod
    def _find_in(go, n):
        if go.name == n: return go
        for ch in go.transform:
            hit = GameObject._find_in(ch.gameObject, n)
            if hit: return hit
        return None
    @staticmethod
    def FindGameObjectWithTag(t):
        for r in _Registry.roots:
            hit = GameObject._find_tag(r, t)
            if hit: return hit
        return None
    @staticmethod
    def _find_tag(go, t):
        if go.tag == t: return go
        for ch in go.transform:
            hit = GameObject._find_tag(ch.gameObject, t)
            if hit: return hit
        return None
    def SendMessageUpwards(self, method):
        tr = self.transform
        while tr is not None:
            for c in tr.gameObject._components:
                if hasattr(c, method): getattr(c, method)()
            tr = tr.parent


class Transform(Component):
    def __init__(self, game_object=None):
        super().__init__(game_object)
        self.localPosition = Vector3.zero()
        self.localRotation = Quaternion.identity()
        self.localScale = Vector3.one()
        self._parent = None
        self._children = []
    @property
    def parent(self): return self._parent
    @parent.setter
    def parent(self, p): self.SetParent(p)
    @property
    def position(self):
        return self.localPosition if self._parent is None else self._parent.TransformPoint(self.localPosition)
    @position.setter
    def position(self, v):
        self.localPosition = v if self._parent is None else self._parent.InverseTransformPoint(v)
    @property
    def rotation(self):
        return self.localRotation if self._parent is None else self._parent.rotation * self.localRotation
    @rotation.setter
    def rotation(self, v):
        if self._parent is None: self.localRotation = v
        else:
            inv = Quaternion(-self._parent.rotation.x, -self._parent.rotation.y,
                             -self._parent.rotation.z, self._parent.rotation.w)
            self.localRotation = inv * v
    @property
    def forward(self): return self.rotation * Vector3.forward()
    @forward.setter
    def forward(self, v): self.LookAt(self.position + v)
    @property
    def up(self): return self.rotation * Vector3.up()
    @property
    def lossyScale(self):
        s = self.localScale
        p = self._parent
        while p is not None:
            s = Vector3(s.x*p.localScale.x, s.y*p.localScale.y, s.z*p.localScale.z)
            p = p._parent
        return s
    def SetParent(self, p):
        if self._parent is not None and self in self._parent._children:
            self._parent._children.remove(self)
        self._parent = p
        if p is not None: p._children.append(self)
    def Translate(self, t): self.localPosition = self.localPosition + t
    def Rotate(self, euler): self.localRotation = self.localRotation * Quaternion.Euler(euler)
    def RotateAround(self, point, axis, angle):
        q = Quaternion.AngleAxis(angle, axis)
        self.position = point + q * (self.position - point)
        self.rotation = q * self.rotation
    def LookAt(self, target):
        tgt = target.position if isinstance(target, Transform) else target
        self.rotation = Quaternion.LookRotation(tgt - self.position)
    def TransformPoint(self, p):
        s = self.localScale
        return self.position + self.rotation * Vector3(p.x*s.x, p.y*s.y, p.z*s.z)
    def TransformDirection(self, d): return self.rotation * d
    def InverseTransformPoint(self, world):
        r = self.rotation
        inv = Quaternion(-r.x, -r.y, -r.z, r.w)
        d = inv * (world - self.position)
        s = self.lossyScale
        return Vector3(d.x/s.x, d.y/s.y, d.z/s.z)
    def __iter__(self): return iter(list(self._children))
    def GetEnumerator(self): return self.__iter__()


# --- atributos (no-ops) e enums (só declarar) ---
class SerializeField: pass
class HeaderAttribute:
    def __init__(self, h): pass
class HideInInspector: pass
class RangeAttribute:
    def __init__(self, mn, mx): pass
class SpaceAttribute:
    def __init__(self, h=8): pass
class TextAreaAttribute: pass
class RequireComponent:
    def __init__(self, t): pass
class ExecuteInEditMode: pass


class KeyCode: W, A, S, D, Space, Escape = 1, 2, 3, 4, 5, 6
class Space: World, Self = 0, 1
class WrapMode: Clamp, Loop, PingPong = 0, 1, 2
class TouchPhase: Began, Moved, Ended = 0, 1, 2
class RenderMode: Overlay, Camera, World = 0, 1, 2
class TextAnchor: UpperLeft, MiddleCenter = 0, 1
class ForceMode: Force, Impulse = 0, 1
class RuntimePlatform: WindowsPlayer, Android = 0, 1
class LoadSceneMode: Single, Additive = 0, 1
