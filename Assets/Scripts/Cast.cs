/* 
 * This file is part of Unity-Procedural-IK-Wall-Walking-Spider on github.com/PhilS94
 * Copyright (C) 2020 Philipp Schofield - All Rights Reserved
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Raycasting {

    public enum CastMode {
        RayCast,
        SphereCast
    }

    /*
     * Class representing a position relative to a given parent transform.
     * Basically mimics parenting an object to a parent, but only for position, without creating new transforms.
     */
public class PositionRelative {
        private Vector3 position;
        private Transform parent;

        public PositionRelative(Vector3 m_position) {
            position = m_position;
            parent = null;
        }

        public PositionRelative(Vector3 m_position, Transform m_parent) {
            parent = m_parent;
            setWorldPosition(m_position);
        }

        public Vector3 getWorldPosition() { return (parent == null) ? position : parent.TransformPoint(position); }
        public Vector3 getLocalPosition() { return position; }
        public Transform getParent() { return parent; }
        public void setWorldPosition(Vector3 m_position) { position = (parent == null) ? m_position : parent.InverseTransformPoint(m_position); }
        public void setLocalPosition(Vector3 m_position) { position = m_position; }
        public void setParent(Transform m_parent) {
            Vector3 old_Position = getWorldPosition();
            parent = m_parent;
            setWorldPosition(old_Position);
        }
    }

    /*
     * Abstract class representing a raycast by origin and end point.
     * Provides us with functions for casting as well as debug drawing.
     * The power of this class is that the origin and end points are relative positions, which enables us to parent them to
     * other transforms. Thus, if a raycasts origin and end point are always fixed relative to a certain transform,
     * the raycast would only need to be initialized once and never in need for an update.
     * 
     * The abstractness follows from the fact that a cast can either be a raycast or a spherecast,
     * each with their own casting and debug drawing functions. Both classes are implemented below.
     */
    public abstract class Cast {

        protected PositionRelative origin;
        protected PositionRelative end;

        public Cast() {
            origin = new PositionRelative(Vector3.zero);
            end = new PositionRelative(Vector3.zero);
        }

        public Cast(Vector3 m_origin, Vector3 m_direction, float m_distance) {
            origin = new PositionRelative(m_origin);
            end = new PositionRelative(m_origin + m_direction.normalized * m_distance);
        }

        public Cast(Vector3 m_origin, Vector3 m_end) {
            origin = new PositionRelative(m_origin);
            end = new PositionRelative(m_end);
        }

        public Cast(Vector3 m_origin, Vector3 m_direction, float m_distance, Transform m_parentOrigin, Transform m_parentEnd) {
            origin = new PositionRelative(m_origin, m_parentOrigin);
            end = new PositionRelative(m_origin + m_direction.normalized * m_distance, m_parentEnd);
        }

        public Cast(Vector3 m_origin, Vector3 m_end, Transform m_parentOrigin, Transform m_parentEnd) {
            origin = new PositionRelative(m_origin, m_parentOrigin);
            end = new PositionRelative(m_end, m_parentEnd);
        }

        // Returns values in World Space
        public Vector3 getOrigin() { return origin.getWorldPosition(); }
        public Vector3 getDirection() { return (end.getWorldPosition() - origin.getWorldPosition()); }
        public float getDistance() { return getDirection().magnitude; }
        public Vector3 getEnd() { return end.getWorldPosition(); }

        public void setParentOrigin(Transform m_parent) { origin.setParent(m_parent); }
        public void setParentEnd(Transform m_parent) { end.setParent(m_parent); }
        public void setOrigin(Vector3 m_Origin) { origin.setWorldPosition(m_Origin); }
        public void setEnd(Vector3 m_end) { end.setWorldPosition(m_end); }
        public void setDistance(float m_distance) { Vector3 p = getOrigin(); end.setWorldPosition(p + (getEnd() - p).normalized * m_distance); }
        public abstract bool castRay(out RaycastHit hitInfo, LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract RaycastHit[] castRayAll(LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore);

        public abstract void draw(Color col, float duration=0);
    }


    public class RayCast : Cast {

        public RayCast() : base() { }

        public RayCast(Vector3 m_origin, Vector3 m_direction, float m_distance) : base(m_origin, m_direction, m_distance) { }

        public RayCast(Vector3 m_origin, Vector3 m_end) : base(m_origin, m_end) { }

        public RayCast(Vector3 m_origin, Vector3 m_direction, float m_distance, Transform m_parentOrigin, Transform m_parentEnd) : base(m_origin, m_direction, m_distance, m_parentOrigin, m_parentEnd) { }

        public RayCast(Vector3 m_origin, Vector3 m_end, Transform m_parentOrigin, Transform m_parentEnd) : base(m_origin, m_end, m_parentOrigin, m_parentEnd) { }


        public override bool castRay(out RaycastHit hitInfo, LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.Raycast(getOrigin(), v.normalized, out hitInfo, v.magnitude, layerMask, q);
        }

        public override RaycastHit[] castRayAll(LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.RaycastAll(getOrigin(), v, v.magnitude, layerMask, q);
        }

        public override void draw(Color col, float duration=0) {
            Debug.DrawLine(getOrigin(), getEnd(), col,duration);
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

        public SphereCast(Vector3 m_origin, Vector3 m_direction, float m_distance, float m_radius,Transform m_parentOrigin, Transform m_parentEnd) : base(m_origin, m_direction, m_distance, m_parentOrigin, m_parentEnd) {
            setRadius(m_radius);
        }

        public SphereCast(Vector3 m_origin, Vector3 m_end, float m_radius, Transform m_parentOrigin,Transform m_parentEnd) : base(m_origin, m_end, m_parentOrigin, m_parentEnd) {
            setRadius(m_radius);
        }

        // Transform radius with origin Transforms lossyscale.z
        public float getRadius() { return (origin.getParent() == null) ? radius : origin.getParent().lossyScale.z * radius; }

        public void setRadius(float m_radius) { radius = (origin.getParent() == null) ? m_radius : m_radius / origin.getParent().lossyScale.z; }

        public override bool castRay(out RaycastHit hitInfo, LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.SphereCast(getOrigin(), getRadius(), v.normalized, out hitInfo, v.magnitude, layerMask, q);
        }

        public override RaycastHit[] castRayAll(LayerMask layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.Ignore) {
            Vector3 v = getDirection();
            return Physics.SphereCastAll(getOrigin(), getRadius(), v.normalized, v.magnitude, layerMask, q);
        }

        public override void draw(Color col,float duration=0) {
            DebugShapes.DrawSphereRay(getOrigin(), getEnd(), getRadius(), 5, col,duration);
        }
    }
}