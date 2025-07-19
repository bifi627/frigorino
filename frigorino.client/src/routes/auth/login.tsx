import { Button } from "@mui/material";
import { createFileRoute } from "@tanstack/react-router";
import { getAuth, signInWithEmailAndPassword, signOut } from "firebase/auth";
import { useCallback, useEffect, useState } from "react";
import { ClientApi } from "../../common/apiClient";
import { useAuthStore } from "../../common/authProvider";
import { type WeatherForecast } from "../../lib/api";

export const Route = createFileRoute("/auth/login")({
    component: App,
});

function App() {
    const [forecasts, setForecasts] = useState<WeatherForecast[]>();

    const auth = getAuth();

    const currentUser = useAuthStore().user;

    const login = async () => {
        await signInWithEmailAndPassword(auth, "test@test.de", "test123");
    };
    const logout = async () => {
        await signOut(auth);
    };

    const populateWeatherData = useCallback(async () => {
        setForecasts([]);
        const reponse = await ClientApi.weatherForecast.getWeatherForecast();
        setForecasts(reponse);
    }, []);

    useEffect(() => {
        populateWeatherData();
    }, [currentUser, populateWeatherData]);

    if (useAuthStore().loading) {
        return <div>Loading...</div>;
    }

    return (
        <div>
            {currentUser ? (
                <Button onClick={logout}>Logout</Button>
            ) : (
                <Button variant="contained" onClick={login}>
                    Login
                </Button>
            )}
            <h1 id="tableLabel">Weather forecast</h1>
            <p>This component demonstrates fetching data from the server.</p>
            <table className="table table-striped" aria-labelledby="tableLabel">
                <thead>
                    <tr>
                        <th>Date</th>
                        <th>Temp. (C)</th>
                        <th>Temp. (F)</th>
                        <th>Summary</th>
                    </tr>
                </thead>
                <tbody>
                    {forecasts?.map((forecast) => (
                        <tr key={forecast.date}>
                            <td>{forecast.date}</td>
                            <td>{forecast.temperatureC}</td>
                            <td>{forecast.temperatureF}</td>
                            <td>{forecast.summary}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

export default App;
