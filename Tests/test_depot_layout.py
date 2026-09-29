"""Checks the supplied metre-based layout and the integration's concrete placements.
Not a substitute for Unity physics, rendering or multiplayer testing.
"""
import gzip,json,math,unittest
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
DATA=json.loads(gzip.decompress((ROOT/'Assets/DepoLevel/Data/DepoLayout.json.gz').read_bytes()))
NODES=DATA['nodes']; BY={n['id']:n for n in NODES}
def matrix(n):
    x,y=map(math.radians,[n.get('rx',0),n.get('ry',0)])
    rx=np.array([[1,0,0],[0,math.cos(x),-math.sin(x)],[0,math.sin(x),math.cos(x)]])
    ry=np.array([[math.cos(y),0,math.sin(y)],[0,1,0],[-math.sin(y),0,math.cos(y)]])
    m=np.eye(4);m[:3,:3]=ry@rx;m[:3,3]=[n.get('px',0),n.get('py',0),n.get('pz',0)]
    return m
WORLD={}
for n in NODES:WORLD[n['id']]=(WORLD[n['parent']] if n.get('parent') else np.eye(4))@matrix(n)
COLLIDERS=[]
for n in NODES:
    if n['kind'] not in ('box','cyl') or n.get('nc')==1:continue
    m=WORLD[n['id']];half=abs(m[:3,:3])@np.array([n.get('sx',0),n.get('sy',0),n.get('sz',0)])/2
    COLLIDERS.append((n,m[:3,3]-half,m[:3,3]+half))
def overlaps(center,half):
    center=np.array(center);half=np.array(half)
    return [n['id'] for n,lo,hi in COLLIDERS if np.all(center+half>lo+1e-5) and np.all(center-half<hi-1e-5)]
def point(node,local):return (WORLD[node]@np.array([*local,1]))[:3]
class DepotLayoutTests(unittest.TestCase):
    def test_identity_and_counts(self):
        self.assertEqual(len(BY),len(NODES));self.assertEqual(sum(n['kind']=='slot' for n in NODES),5987)
        self.assertEqual(sum(n.get('tag')=='container' for n in NODES),10)
    def test_four_spawns_and_central_corridor(self):
        for x in (-2.4,-.8,.8,2.4):self.assertEqual(overlaps((x,1,-75.5),(.3,.85,.3)),[])
        for z in np.arange(-75.5,1,.5):self.assertEqual(overlaps((0,1,z),(.3,.7,.3)),[],f'corridor z={z}')
    def test_all_crates_reveal_positions(self):
        for n in NODES:
            if n.get('tag')!='container':continue
            for i in range(30):
                center=point(n['id'],(4+(i%5)*.48,.39,-1.2+(i//5)*.48))
                self.assertEqual(overlaps(center,(.22,.21,.17)),[],f"{n['name']} item {i}")
    def test_twelve_racks_and_120_cells(self):
        racks=sorted([n for n in NODES if n.get('prefab')=='Raf_Standart'],key=lambda n:(n['px']**2+n['pz']**2,n['id']))[:12]
        self.assertEqual(len(racks),12)
        for rack in racks:
            cells=[n for n in NODES if n.get('parent')==rack['id'] and n['kind']=='slot'];self.assertEqual(len(cells),10)
            approach=point(rack['id'],(0,1,-1.3));self.assertEqual(overlaps(approach,(.3,.7,.3)),[])
            for n in cells:
                m=WORLD[n['id']];center=m[:3,3]+np.array([0,-n['sy']/2+.19,0]);half=abs(m[:3,:3])@np.array([.22,.21,.17])*.85
                self.assertEqual(overlaps(center,half),[],f"{rack['name']} {n['name']}")
    def test_recovery_grid_has_room_for_all_300(self):
        container=next(n for n in NODES if n.get('tag')=='container' and n['data']=='id=10')
        for i in range(300):
            center=point(container['id'],(12+(i//6)*.46,.39,-1.5+(i%6)*.6))
            self.assertEqual(overlaps(center,(.22,.21,.17)),[])
if __name__=='__main__':unittest.main()
