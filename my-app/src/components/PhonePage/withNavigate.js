// withNavigate.js
import { useNavigate } from 'react-router-dom';
import React from 'react';

export function withNavigate(Component) {
    function WrappedComponent(props) {
        const navigate = useNavigate();
        return <Component {...props} navigate={navigate} />;
    }
    return WrappedComponent;
}