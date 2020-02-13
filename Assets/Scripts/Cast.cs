using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raycasting {

    public enum CastMode {
        RayCast,
        SphereCast
    }

    public abstract class Cast {

        //If parent set, then parameters are in Local Space of the Parent
        protected Transform parent;
        protected Vector3 origin;
        protected Vector3 direction;

        public Cast() {
            origin = Vector3.zero;
            direction = -Vector3.up;
        }

        public Cast(Vector3 m_origin, Vector3 m_direction, float m_distance) {
            origin = m_origin;
            direction = m_direction.normalized *m_distance;
        }

        public Cast(Vector3 m_origin, Vector3 m_end) {
            origin = m_origin;
            direction = (m_end - m_origin);
        }

        public Cast(Vector3 m_origin, Vector3 m_direction, float m_distance, Transform m_parent) {
            parent = m_parent;
            setOrigin(m_origin);
            setDirection(m_direction.normalized * m_distance);
        }

        public Cast(Vector3 m_origin, Vector3 m_end, Transform m_parent) {
            parent = m_parent;
            setOrigin(m_origin);
            setDirection(m_end - m_origin);
        }

        // Returns values in World Space
        public Vector3 getOrigin() { return (parent == null) ? origin : parent.TransformPoint(origin); }
        public Vector3 getDirection() { return (parent == null) ? direction : parent.TransformVector(direction); }
        public float getDistance() { return getDirection().magnitude; }
        public Vector3 getEnd() { return getOrigin() + getDirection(); }

        public void setParent(Transform m_parent) {
            if (parent == null) {
                // Dont have a current parent, therefore current origin and direction are already in world coordinates.
                parent = m_parent;
                setOrigin(origin);
                setDirection(direction);
            }
            else {
                // Already have a parent. Get the current world positions, and set them again after changing parent.
                Vector3 oldOriginWorldSpace = getOrigin();
                Vector3 oldDirectionWorldSpace = getDirection();
                parent = m_parent;
                setOrigin(oldOriginWorldSpace);
                setDirection(oldDirectionWorldSpace);

            }
            parent = m_parent;
        }

        // Input parameters are in World Space
        public void setOrigin(Vector3 m_Origin) { origin = (parent == null) ? m_Origin : parent.InverseTransformPoint(m_Origin); }
        public void setDirection(Vector3 m_Direction) { direction = (parent == null) ? m_Direction : parent.InverseTransformVector(m_Direction); }
        public void setLookDirection(Vector3 point, float distance) { setDirection((point - getOrigin()).normalized * distance); }
        public void setDistance(float m_Distance) { setDirection(getDirection().normalized * m_Distance); }
        public void setEnd(Vector3 m_end) { setDirection(m_end - getOrigin()); }
        public void setSymmetricThroughCenter(Vector3 center, Vector3 normal, float height) {
            setOrigin(center + height * normal);
            setDirection(2 * height * -normal);
        }
        public abstract bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract void draw(Color col);
    }


    public class RayCast : Cast {

        public RayCast() : base() { }

        public RayCast(Vector3 m_origin, Vector3 m_direction, float m_distance) : base(m_origin, m_direction, m_distance) { }

        public RayCast(Vector3 m_origin, Vector3 m_end) : base(m_origin, m_end) { }

        public RayCast(Vector3 m_origin, Vector3 m_direction, float m_distance, Transform m_parent) : base(m_origin, m_direction, m_distance, m_parent) { }

        public RayCast(Vector3 m_origin, Vector3 m_end, Transform m_parent) : base(m_origin, m_end, m_parent) { }


        public override bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.Raycast(getOrigin(), v.normalized, out hitInfo, v.magnitude, layerMask, q);
        }

        public override RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.RaycastAll(getOrigin(), v, v.magnitude, layerMask, q);
        }

        public override void draw(Color col) {
            Debug.DrawLine(getOrigin(), getEnd(), col);
        }
    }


    public class SphereCast : Cast {
        protected float radius;

        public SphereCast() : base() {
            radius = 1.0f;
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

        public SphereCast(Vector3 m_origin, Vector3 m_direction, float m_distance, float m_radius, Transform m_parent) : base(m_origin, m_direction, m_distance,m_parent) {
            setRadius(m_radius);
        }

        public SphereCast(Vector3 m_origin, Vector3 m_end, float m_radius, Transform m_parent) : base(m_origin, m_end,m_parent) {
            setRadius(m_radius);
        }

        public float getRadius() { return (parent == null) ? radius : parent.lossyScale.z * radius; }

        public void setRadius(float m_radius) { radius = (parent == null) ? m_radius : m_radius / parent.lossyScale.z; }

        public override bool castRay(out RaycastHit hitInfo, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.SphereCast(getOrigin(), getRadius(), v.normalized, out hitInfo, v.magnitude, layerMask, q);
        }

        public override RaycastHit[] castRayAll(int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.SphereCastAll(getOrigin(), getRadius(), v.normalized, v.magnitude, layerMask, q);
        }

        public override void draw(Color col) {
            DebugShapes.DrawSphereRay(getOrigin(), getEnd(), getRadius(), 5, col);
        }
    }
}