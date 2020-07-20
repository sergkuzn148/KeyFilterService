import svelte from 'rollup-plugin-svelte';
import resolve from '@rollup/plugin-node-resolve';
import commonjs from '@rollup/plugin-commonjs';
import json from '@rollup/plugin-json';
import css from 'rollup-plugin-css-porter';
import babel from 'rollup-plugin-babel';
import { terser } from 'rollup-plugin-terser';
import pkg from './package.json';

export default {
        input: './Client/index.js',
        output: {
            file: pkg.main,
            format: 'iife',
            sourcemap: true,
            name: 'AIS',
        },
        plugins: [
            svelte(),
            resolve(),
            commonjs(),
            json(),
            css({dest: 'Server/wwwroot/main.css', minified: true}),
            babel({
                extensions: ['.js', '.mjs'],
                exclude: ['node_modules/@babel/**', 'node_modules/core-js/**'],
                include: ['Client/**'],
            }),
            terser(),
        ]
    };
