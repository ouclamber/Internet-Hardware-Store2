import React from 'react';
import './App.css';
import SignUp from './components/SignUp';
import SignIn from './components/SignIn';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import HomePage from './components/Home/HomePage';
import Profile from './components/Profile/Profile';
import Television from './components/TelevisionPage/Television';
import Labtop from './components/LabtopPage/Labtop';
import Computer from './components/ComputerPage/Computer';
import Speaker from './components/SpeakerPage/Speaker';
import Phone from './components/PhonePage/Phone';
import ProductPage from './components/ProductPage/ProductPage';
import Cart from './components/CartPage/Cart';
import CartProvider from './components/CartContext/CartContext';
import SearchPage from './components/SearchPage/SearchPage';
import CheckoutPage from './components/CheckoutPage/CheckoutPage';
import AdminPanel from './components/AdminPanel/AdminPanel';

function App() {
  return (
    <div className="App">
      <CartProvider>
        <Router>
          <Routes>
            <Route path="/" element={<SignIn/>}/>
            <Route path="/SignUp" element={<SignUp/>}/>
            <Route path="/SignIn" element={<SignIn/>}/>
            <Route path="/HomePage" element={<HomePage/>}/>
            <Route path="/Profile" element={<Profile/>}/>
            <Route path="/Television" element={<Television/>}/>
            <Route path="/Labtop" element={<Labtop/>}/>
            <Route path="/Computer" element={<Computer/>}/>
            <Route path="/Speaker" element={<Speaker/>}/>
            <Route path="/Phone" element={<Phone/>}/>
            <Route path="/product/:id" element={<ProductPage/>}/>
            <Route path="/Cart" element={<Cart/>}/>
            <Route path="/search" element={<SearchPage/>}/>
            <Route path="/checkout" element={<CheckoutPage/>}/>
            <Route path="/Admin" element={<AdminPanel/>} />
          </Routes>
        </Router>
      </CartProvider>
    </div>
  );
};

export default App;