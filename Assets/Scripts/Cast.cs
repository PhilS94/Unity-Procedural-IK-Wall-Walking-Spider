using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raycasting {

    public enum CastMode {
        RayCast,
        SphereCast
    }

    public abstract class Cast {
        protected Vector3 origin;
        protected Vector3 direction;
        protected float distance;

        public Cast() {
            origin = Vector3.zero;
            direction = Vector3.zero;
            distance = 0;
        }

        public Cast(Vector3 m_origin, Vector3 m_end) {
            origin = m_origin;
            direction = (m_end - m_origin).normalized;
        }

        public Cast(Vector3 m_origin, Vector3 m_direction, float m_distance) {
            origin = m_origin;
            direction = m_direction;
            distance = m_distance;
        }

        public Vector3 getOrigin() { return origin; }
        public Vector3 getDirection() { return direction; }
        public float getDistance() { return distance; }
        public Vector3 getEnd() { return origin + distance * direction; }

        public void setOrigin(Vector3 m_Origin) { origin = m_Origin; }
        public void setDirection(Vector3 m_Direction) { direction = m_Direction; }
        public void setDistance(float m_distance) { distance = m_distance; }
        public void setDirectionDistance(Vector3 v) { direction = v.normalized; distance = v.magnitude; }
        public void setLookDirection(Vector3 point) { direction = (point - origin).normalized; }
        public void setEnd(Vector3 m_end) { direction = (m_end - origin).normalized; distance = (m_end - origin).magnitude; }
        public void setSymmetricThroughCenter(Vector3 center, Vector3 normal, float height) {
            origin = center + height * normal;
            setDirectionDistance(2 * height * -normal);
        }
        public abstract bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract void draw(Color col);
    }


    public class RayCast : Cast {

        public RayCast() : base() { }

        public RayCast(Vector3 m_origin, Vector3 m_end) : base(m_origin, m_end) { }

        public RayCast(Vector3 m_origin, Vector3 m_direction, float m_distance) : base(m_origin, m_direction, m_distance) { }

        public override bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            // could check if the normal is acceptable
            return Physics.Raycast(origin, direction, out hitInfo, distance, layerMask, q);
        }

        public override RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            return Physics.RaycastAll(origin, direction, distance, layerMask, q);
        }

        public override void draw(Color col) {
            Debug.DrawLine(origin, getEnd(), col);
        }
    }


    public class SphereCast : Cast {
        protected float radius;

        public SphereCast() : base() {
            radius = 0;
        }

        public SphereCast(float m_radius) : base() {
            radius = m_radius;
        }

        public SphereCast(Vector3 m_origin, Vector3 m_direction, float m_distance, float m_radius) : base(m_origin, m_direction, m_distance) {
            radius = m_radius;
        }

        public SphereCast(Vector3 m_origin, Vector3 m_end, float m_radius) : base(m_origin, m_end) {
            radius = m_radius;
        }

        public float getRadius() { return radius; }

        public void setRadius(float m_radius) { radius = m_radius; }

        public override bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            // could check if the normal is acceptable
            return Physics.SphereCast(origin, radius, direction, out hitInfo, distance, layerMask, q);
        }

        public override RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            return Physics.SphereCastAll(origin, radius, direction, distance, layerMask, q);
        }

        public override void draw(Color col) {
            DebugShapes.DrawSphereRay(origin, getEnd(), radius, 5, col);
        }
    }
}